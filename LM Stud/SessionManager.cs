using System;
using System.Collections.Generic;
using System.Linq;
namespace LMStud{
	internal class SessionManager{
		private readonly int _maxSessions;
		private readonly int _maxTokens;
		private readonly Action<string> _onRemoved;
		private readonly Dictionary<string, Session> _sessions = new Dictionary<string, Session>();
		public SessionManager(int maxSessions = 32, int maxTokens = 128000, Action<string> onRemoved = null){
			_maxSessions = maxSessions;
			_maxTokens = maxTokens;
			_onRemoved = onRemoved;
		}
		private int TotalTokens => _sessions.Values.Sum(s => s.TokenCount);
		public Session Get(string id){
			if(!string.IsNullOrEmpty(id) && _sessions.TryGetValue(id, out var sess)){
				sess.LastUsed = DateTime.UtcNow;
				return sess;
			}
			var newSession = new Session{ Id = string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString() : id, LastUsed = DateTime.UtcNow };
			_sessions[newSession.Id] = newSession;
			Evict();
			return newSession;
		}
		public void Update(Session session, int tokenCount){
			session.TokenCount = tokenCount;
			session.LastUsed = DateTime.UtcNow;
			Evict();
		}
		public void Remove(string id){
			if(string.IsNullOrEmpty(id)) return;
			if(!_sessions.Remove(id)) return;
			_onRemoved?.Invoke(id);
		}
		private void Evict(){
			while(_sessions.Count > _maxSessions || TotalTokens > _maxTokens){
				var lru = _sessions.Values.OrderBy(s => s.LastUsed).First();
				_sessions.Remove(lru.Id);
				_onRemoved?.Invoke(lru.Id);
			}
		}
		internal class Session{
			public string Id = Guid.NewGuid().ToString();
			public DateTime LastUsed;
			public List<ApiServer.Message> Messages = new List<ApiServer.Message>();
			public int TokenCount;
		}
	}
}