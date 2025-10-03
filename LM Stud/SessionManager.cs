using System;
using System.Collections.Generic;
using System.Linq;
namespace LMStud{
	internal class SessionManager{
		private readonly int _maxSessions;
		private readonly int _maxTokens;
		private readonly Dictionary<string, Session> _sessions = new Dictionary<string, Session>();
		private readonly object _sync = new object();
		public SessionManager(int maxSessions = 32, int maxTokens = 128000){
			_maxSessions = maxSessions;
			_maxTokens = maxTokens;
		}
		private int TotalTokens => _sessions.Values.Sum(s => s.TokenCount);
		public Session Get(string id){
			lock(_sync){
				if(!string.IsNullOrEmpty(id) && _sessions.TryGetValue(id, out var sess)){
					sess.LastUsed = DateTime.UtcNow;
					return sess;
				}
				var newSession = new Session{ Id = string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString() : id, LastUsed = DateTime.UtcNow };
				_sessions[newSession.Id] = newSession;
				Evict();
				return newSession;
			}
		}
		public void Update(Session session, List<ApiServer.Message> messages, byte[] state, int tokenCount){
			lock(_sync){
				session.Messages = messages;
				session.State = state;
				session.TokenCount = tokenCount;
				session.LastUsed = DateTime.UtcNow;
				Evict();
			}
		}
		public void Remove(string id){
			if(string.IsNullOrEmpty(id)) return;
			lock(_sync){ _sessions.Remove(id); }
		}
		private void Evict(){
			while(_sessions.Count > _maxSessions || _sessions.Values.Sum(s => s.TokenCount) > _maxTokens){
				var lru = _sessions.Values.OrderBy(s => s.LastUsed).First();
				_sessions.Remove(lru.Id);
			}
		}
		internal class Session{
			public string Id = Guid.NewGuid().ToString();
			public DateTime LastUsed;
			public List<ApiServer.Message> Messages = new List<ApiServer.Message>();
			public byte[] State;
			public int TokenCount;
		}
	}
}