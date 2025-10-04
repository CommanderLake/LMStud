using System;
using System.Collections.Generic;
using System.Threading;
using LMStud;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace LM_Stud.Tests{
	[TestClass]
	public class SessionManagerTests{
		private SessionManager _sessionManager;
		[TestInitialize]
		public void TestInitialize(){_sessionManager = new SessionManager(3, 1000);}
		[TestMethod]
		public void Get_WithNewId_CreatesNewSession(){
			var sessionId = "test-session-1";
			var session = _sessionManager.Get(sessionId);
			Assert.IsNotNull(session, "Session should be created.");
			Assert.AreEqual(sessionId, session.Id, "Session ID should match.");
			Assert.IsNotNull(session.Messages, "Messages list should be initialized.");
			Assert.AreEqual(0, session.Messages.Count, "New session should have no messages.");
		}
		[TestMethod]
		public void Get_WithExistingId_ReturnsExistingSession(){
			var sessionId = "test-session-1";
			var session1 = _sessionManager.Get(sessionId);
			session1.TokenCount = 100;
			var session2 = _sessionManager.Get(sessionId);
			Assert.AreSame(session1, session2, "Should return the same session instance.");
			Assert.AreEqual(100, session2.TokenCount, "Session data should be preserved.");
		}
		[TestMethod]
		public void Get_WithNullId_CreatesSessionWithGeneratedId(){
			var session = _sessionManager.Get(null);
			Assert.IsNotNull(session, "Session should be created.");
			Assert.IsFalse(string.IsNullOrEmpty(session.Id), "Session should have generated ID.");
		}
		[TestMethod]
		public void Get_WithEmptyId_CreatesSessionWithGeneratedId(){
			var session = _sessionManager.Get("");
			Assert.IsNotNull(session, "Session should be created.");
			Assert.IsFalse(string.IsNullOrEmpty(session.Id), "Session should have generated ID.");
		}
		[TestMethod]
		public void Update_ModifiesSessionData(){
			var sessionId = "test-session-1";
			var session = _sessionManager.Get(sessionId);
			var messages = new List<ApiServer.Message>{ new ApiServer.Message{ Role = "user", Content = "test" } };
			var state = new byte[]{ 1, 2, 3 };
			var tokenCount = 50;
			_sessionManager.Update(session, messages, state, tokenCount);
			Assert.AreEqual(1, session.Messages.Count, "Messages should be updated.");
			Assert.AreEqual("test", session.Messages[0].Content, "Message content should match.");
			CollectionAssert.AreEqual(state, session.State, "State should be updated.");
			Assert.AreEqual(tokenCount, session.TokenCount, "Token count should be updated.");
		}
		[TestMethod]
		public void Remove_WithExistingId_RemovesSession(){
			var sessionId = "test-session-1";
			var session1 = _sessionManager.Get(sessionId);
			_sessionManager.Remove(sessionId);
			var session2 = _sessionManager.Get(sessionId);
			Assert.AreNotSame(session1, session2, "Should create new session after removal.");
		}
		[TestMethod]
		public void Remove_WithNullId_DoesNothing(){
			_sessionManager.Remove(null);// Should not throw
		}
		[TestMethod]
		public void Remove_WithEmptyId_DoesNothing(){
			_sessionManager.Remove("");// Should not throw
		}
		[TestMethod]
		public void Evict_WhenExceedsMaxSessions_RemovesOldestSessions(){
			// Create sessions up to max limit
			var session1 = _sessionManager.Get("session-1");
			Thread.Sleep(10);
			var session2 = _sessionManager.Get("session-2");
			Thread.Sleep(10);
			var session3 = _sessionManager.Get("session-3");
			Thread.Sleep(10);

			// Creating one more should evict the oldest
			var session4 = _sessionManager.Get("session-4");

			// Check that session1 was evicted
			var retrievedSession1 = _sessionManager.Get("session-1");
			Assert.AreNotSame(session1, retrievedSession1, "Oldest session should be evicted and recreated.");

			// Check that newer sessions still exist
			var retrievedSession3 = _sessionManager.Get("session-3");
			Assert.AreSame(session3, retrievedSession3, "Newer session should still exist.");
		}
		[TestMethod]
		public void Evict_WhenExceedsMaxTokens_RemovesSessionsToFitLimit(){
			var session1 = _sessionManager.Get("session-1");
			_sessionManager.Update(session1, new List<ApiServer.Message>(), null, 400);
			Thread.Sleep(10);
			var session2 = _sessionManager.Get("session-2");
			_sessionManager.Update(session2, new List<ApiServer.Message>(), null, 400);
			Thread.Sleep(10);

			// This should trigger eviction due to token limit
			var session3 = _sessionManager.Get("session-3");
			_sessionManager.Update(session3, new List<ApiServer.Message>(), null, 300);

			// Session1 should be evicted (oldest)
			var retrievedSession1 = _sessionManager.Get("session-1");
			Assert.AreNotSame(session1, retrievedSession1, "Session should be evicted when token limit exceeded.");
		}
		[TestMethod]
		public void LastUsed_UpdatedOnGet(){
			var sessionId = "test-session-1";
			var session = _sessionManager.Get(sessionId);
			var initialTime = session.LastUsed;
			Thread.Sleep(50);
			_sessionManager.Get(sessionId);
			Assert.IsTrue(session.LastUsed > initialTime, "LastUsed should be updated on Get.");
		}
		[TestMethod]
		public void LastUsed_UpdatedOnUpdate(){
			var sessionId = "test-session-1";
			var session = _sessionManager.Get(sessionId);
			var initialTime = session.LastUsed;
			Thread.Sleep(50);
			_sessionManager.Update(session, new List<ApiServer.Message>(), null, 0);
			Assert.IsTrue(session.LastUsed > initialTime, "LastUsed should be updated on Update.");
		}
		[TestMethod]
		public void ConcurrentAccess_HandledSafely(){
			var exceptions = new List<Exception>();
			var threads = new List<Thread>();
			for(var i = 0; i < 10; i++){
				var thread = new Thread(() => {
					try{
						for(var j = 0; j < 10; j++){
							var session = _sessionManager.Get($"session-{Thread.CurrentThread.ManagedThreadId}-{j}");
							_sessionManager.Update(session, new List<ApiServer.Message>(), null, 10);
							Thread.Sleep(1);
						}
					} catch(Exception ex){
						lock(exceptions){ exceptions.Add(ex); }
					}
				});
				threads.Add(thread);
				thread.Start();
			}
			foreach(var thread in threads) thread.Join();
			Assert.AreEqual(0, exceptions.Count, "No exceptions should occur during concurrent access.");
		}
	}
}