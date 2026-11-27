using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
namespace LMStud{
	internal sealed class SessionManager{
		private readonly Dictionary<string, Session> _sessions = new Dictionary<string, Session>(StringComparer.Ordinal);
		private readonly Dictionary<string, Turn> _responses = new Dictionary<string, Turn>(StringComparer.Ordinal);
		private readonly object _sync = new object();

		internal Session Get(string id, bool store){
			lock(_sync){
				if(!string.IsNullOrEmpty(id) && _sessions.TryGetValue(id, out var existing)) return existing;
				var session = new Session(string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString("N") : id);
				if(store){
					_sessions.Add(session.Id, session);
					if(_sessions.Count > 32) RemoveCore(_sessions.Values.Where(item => item != session).OrderBy(item => item.LastUsed).First().Id);
				}
				return session;
			}
		}

		internal Turn FindResponse(string id){
			lock(_sync){ return id != null && _responses.TryGetValue(id, out var turn) ? turn : null; }
		}
		internal bool DeleteResponse(string id){
			lock(_sync){ return _responses.Remove(id); }
		}

		internal bool Save(Session session, Turn turn, byte[] state, IntPtr chatState, bool response){
			lock(_sync){
				if(session.Retired) return false;
				session.SetNativeState(state, chatState);
				session.Latest = turn;
				session.LastUsed = DateTime.UtcNow;
				if(response){
					_responses.Add(turn.Id, turn);
					if(_responses.Count > 256) _responses.Remove(_responses.Values.OrderBy(item => item.Created).First().Id);
				}
				return true;
			}
		}

		internal void Remove(string id){
			lock(_sync){ RemoveCore(id); }
		}
		internal void Clear(){
			lock(_sync){
				foreach(var session in _sessions.Values) Retire(session);
				_sessions.Clear();
				_responses.Clear();
			}
		}
		private void RemoveCore(string id){
			if(!_sessions.TryGetValue(id, out var session)) return;
			_sessions.Remove(id);
			foreach(var response in _responses.Where(item => ReferenceEquals(item.Value.Session, session)).Select(item => item.Key).ToArray())
				_responses.Remove(response);
			Retire(session);
		}
		private static void Retire(Session session){
			session.Retired = true;
			ThreadPool.QueueUserWorkItem(_ => {
				lock(session.SyncRoot){ session.SetNativeState(null, IntPtr.Zero); }
			});
		}

		internal sealed class Session{
			internal readonly string Id;
			internal readonly object SyncRoot = new object();
			internal DateTime LastUsed = DateTime.UtcNow;
			internal volatile bool Retired;
			internal Turn Latest;
			internal byte[] State;
			internal IntPtr NativeChatState;
			internal Session(string id){Id = id;}
			internal void SetNativeState(byte[] state, IntPtr chatState){
				if(NativeChatState != IntPtr.Zero && NativeChatState != chatState) NativeMethods.FreeChatState(NativeChatState);
				State = state;
				NativeChatState = chatState;
			}
		}
		internal sealed class Turn{
			internal readonly Session Session;
			internal readonly string Id;
			internal readonly string Model;
			internal readonly string Backend;
			internal readonly string Instructions;
			internal readonly string ToolsJson;
			internal readonly JsonNode[] Items;
			internal readonly JsonNode Payload;
			internal readonly DateTime Created = DateTime.UtcNow;
			internal Turn(Session session, string id, string model, string backend, string instructions, string toolsJson, JsonNode[] items, JsonNode payload){
				Session = session;
				Id = id;
				Model = model;
				Backend = backend;
				Instructions = instructions;
				ToolsJson = toolsJson;
				Items = items;
				Payload = payload;
			}
		}
	}
}
