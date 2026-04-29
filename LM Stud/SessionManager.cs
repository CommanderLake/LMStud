using System;
using System.Collections.Generic;
using System.Linq;
namespace LMStud{
	internal class SessionManager{
		private readonly int _maxSessions;
		private readonly Dictionary<string, Session> _sessions = new Dictionary<string, Session>();
		private readonly Dictionary<string, string> _responseSessions = new Dictionary<string, string>();
		private readonly object _sync = new object();
		public SessionManager(int maxSessions = 32){
			_maxSessions = maxSessions;
		}
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
		public void Update(Session session, List<APIServer.Message> messages, byte[] state, int tokenCount){
			if(session == null) return;
			lock(_sync){
				session.Messages = CloneMessages(messages);
				session.State = CloneState(state);
				session.ClearNativeChatState();
				session.TokenCount = tokenCount;
				session.LastUsed = DateTime.UtcNow;
				Evict();
			}
		}
		public void Apply(Session session, Action<Session> update){
			if(session == null || update == null) return;
			lock(_sync){
				update(session);
				session.LastUsed = DateTime.UtcNow;
				Evict();
			}
		}
		public string GetSessionIdForResponse(string responseId){
			if(string.IsNullOrWhiteSpace(responseId)) return null;
			lock(_sync){ return _responseSessions.TryGetValue(responseId, out var sessionId) ? sessionId : null; }
		}
		public void RememberResponse(string responseId, Session session){
			if(string.IsNullOrWhiteSpace(responseId) || session == null) return;
			lock(_sync){ _responseSessions[responseId] = session.Id; }
		}
		public void Remove(string id){
			if(string.IsNullOrEmpty(id)) return;
			lock(_sync){
				if(_sessions.TryGetValue(id, out var session)) session.ClearNativeChatState();
				_sessions.Remove(id);
				RemoveResponseIdsForSession(id);
			}
		}
		public void Clear(){
			lock(_sync){
				foreach(var session in _sessions.Values) session.ClearNativeChatState();
				_sessions.Clear();
				_responseSessions.Clear();
			}
		}
		private static List<APIServer.Message> CloneMessages(IEnumerable<APIServer.Message> messages){
			var clone = new List<APIServer.Message>();
			if(messages == null) return clone;
			foreach(var message in messages){
				if(message == null) continue;
				clone.Add(new APIServer.Message{
					Role = message.Role, Content = message.Content, ToolCallId = message.ToolCallId,
					ToolCalls = message.ToolCalls == null ? null : new List<APIClient.ToolCall>(message.ToolCalls)
				});
			}
			return clone;
		}
		private static byte[] CloneState(byte[] state){
			if(state == null) return null;
			var clone = new byte[state.Length];
			Array.Copy(state, clone, state.Length);
			return clone;
		}
		private void Evict(){
			while(_sessions.Count > _maxSessions){
				var lru = _sessions.Values.OrderBy(s => s.LastUsed).First();
				lru.ClearNativeChatState();
				_sessions.Remove(lru.Id);
				RemoveResponseIdsForSession(lru.Id);
			}
		}
		private void RemoveResponseIdsForSession(string sessionId){
			foreach(var responseId in _responseSessions.Where(pair => pair.Value == sessionId).Select(pair => pair.Key).ToList())
				_responseSessions.Remove(responseId);
		}
		internal class Session{
			public string Id = Guid.NewGuid().ToString();
			public DateTime LastUsed;
			public List<APIServer.Message> Messages = new List<APIServer.Message>();
			public byte[] State;
			public int TokenCount;
			public string LastBackend;
			public string ToolsJson;
			public IntPtr NativeChatState;
			public readonly List<APIServer.ToolRecord> ToolRecords = new List<APIServer.ToolRecord>();
			public void SetNativeChatState(IntPtr state){
				if(NativeChatState != IntPtr.Zero && NativeChatState != state) NativeMethods.FreeChatState(NativeChatState);
				NativeChatState = state;
			}
			public void ClearNativeChatState(){ SetNativeChatState(IntPtr.Zero); }
		}
	}
}
