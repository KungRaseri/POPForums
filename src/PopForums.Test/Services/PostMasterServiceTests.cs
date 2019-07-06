﻿using System;
using System.Collections.Generic;
using Moq;
using PopForums.Configuration;
using PopForums.Messaging;
using PopForums.Models;
using PopForums.Repositories;
using PopForums.ScoringGame;
using PopForums.Services;
using Xunit;

namespace PopForums.Test.Services
{
	public class PostMasterServiceTests
	{
		private PostMasterService GetService()
		{
			_textParser = new Mock<ITextParsingService>();
			_topicRepo = new Mock<ITopicRepository>();
			_postRepo = new Mock<IPostRepository>();
			_forumRepo = new Mock<IForumRepository>();
			_profileRepo = new Mock<IProfileRepository>();
			_eventPublisher = new Mock<IEventPublisher>();
			_broker = new Mock<IBroker>();
			_searchIndexQueueRepo = new Mock<ISearchIndexQueueRepository>();
			_tenantService = new Mock<ITenantService>();
			_subscribedTopicsService = new Mock<ISubscribedTopicsService>();
			_moderationLogService = new Mock<IModerationLogService>();
			_forumPermissionService = new Mock<IForumPermissionService>();
			_settingsManager = new Mock<ISettingsManager>();
			_topicViewCountService = new Mock<ITopicViewCountService>();
			return new PostMasterService(_textParser.Object, _topicRepo.Object, _postRepo.Object, _forumRepo.Object, _profileRepo.Object, _eventPublisher.Object, _broker.Object, _searchIndexQueueRepo.Object, _tenantService.Object, _subscribedTopicsService.Object, _moderationLogService.Object, _forumPermissionService.Object, _settingsManager.Object, _topicViewCountService.Object);
		}

		private Mock<ITextParsingService> _textParser;
		private Mock<ITopicRepository> _topicRepo;
		private Mock<IPostRepository> _postRepo;
		private Mock<IForumRepository> _forumRepo;
		private Mock<IProfileRepository> _profileRepo;
		private Mock<IEventPublisher> _eventPublisher;
		private Mock<IBroker> _broker;
		private Mock<ISearchIndexQueueRepository> _searchIndexQueueRepo;
		private Mock<ITenantService> _tenantService;
		private Mock<ISubscribedTopicsService> _subscribedTopicsService;
		private Mock<IModerationLogService> _moderationLogService;
		private Mock<IForumPermissionService> _forumPermissionService;
		private Mock<ISettingsManager> _settingsManager;
		private Mock<ITopicViewCountService> _topicViewCountService;

		private User DoUpNewTopic()
		{
			var forum = new Forum { ForumID = 1 };
			var user = GetUser();
			const string ip = "127.0.0.1";
			const string title = "mah title";
			const string text = "mah text";
			var newPost = new NewPost { Title = title, FullText = text, ItemID = 1 };
			var service = GetService();
			_topicRepo.Setup(t => t.GetUrlNamesThatStartWith("parsed-title")).Returns(new List<string>());
			_textParser.Setup(t => t.ClientHtmlToHtml("mah text")).Returns("parsed text");
			_textParser.Setup(t => t.Censor("mah title")).Returns("parsed title");
			_postRepo.Setup(p => p.Create(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<bool>(), It.IsAny<string>(), null, It.IsAny<bool>(), It.IsAny<int>())).Returns(69);
			_forumRepo.Setup(x => x.Get(forum.ForumID)).Returns(forum);
			_forumRepo.Setup(x => x.GetForumViewRoles(forum.ForumID)).Returns(new List<string>());
			_topicRepo.Setup(x => x.Create(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string>())).Returns(111);
			_forumPermissionService.Setup(x => x.GetPermissionContext(forum, user)).Returns(new ForumPermissionContext {UserCanModerate = false, UserCanPost = true, UserCanView = true});
			service.PostNewTopic(user, newPost, ip, It.IsAny<string>(), x => "", x => "");
			return user;
		}
		
		private User GetUser()
		{
			var user = Models.UserTest.GetTestUser();
			user.Roles = new List<string>();
			return user;
		}

		public class PostNewTopicTests : PostMasterServiceTests
		{
			[Fact]
			public void NoUserReturnsFalseIsSuccess()
			{
				var service = GetService();

				var result = service.PostNewTopic(null, new NewPost(), "", "", x => "", x => "");

				Assert.False(result.IsSuccessful);
			}

			[Fact]
			public void UserWithoutPostPermissionReturnsFalseIsSuccess()
			{
				var service = GetService();
				var forum = new Forum{ForumID = 1};
				_forumRepo.Setup(x => x.Get(forum.ForumID)).Returns(forum);
				var user = GetUser();
				_forumPermissionService.Setup(x => x.GetPermissionContext(forum, user)).Returns(new ForumPermissionContext {DenialReason = Resources.ForumNoPost, UserCanModerate = false, UserCanPost = false, UserCanView = true});

				var result = service.PostNewTopic(user, new NewPost {ItemID = forum.ForumID}, "", "", x => "", x => "");

				Assert.False(result.IsSuccessful);
			}

			[Fact]
			public void UserWithoutViewPermissionReturnsFalseIsSuccess()
			{
				var service = GetService();
				var forum = new Forum { ForumID = 1 };
				_forumRepo.Setup(x => x.Get(forum.ForumID)).Returns(forum);
				var user = GetUser();
				_forumPermissionService.Setup(x => x.GetPermissionContext(forum, user)).Returns(new ForumPermissionContext { DenialReason = Resources.ForumNoView, UserCanModerate = false, UserCanPost = false, UserCanView = false });

				var result = service.PostNewTopic(user, new NewPost { ItemID = forum.ForumID }, "", "", x => "", x => "");

				Assert.False(result.IsSuccessful);
			}

			[Fact]
			public void NoForumMatchThrows()
			{
				var service = GetService();
				_forumRepo.Setup(x => x.Get(It.IsAny<int>())).Returns((Forum) null);

				Assert.Throws<Exception>(() => service.PostNewTopic(GetUser(), new NewPost {ItemID = 1}, "", "", x => "", x => ""));
			}

			[Fact]
			public void CallsPostRepoCreateWithPlainText()
			{
				var forum = new Forum { ForumID = 1 };
				var user = GetUser();
				const string ip = "127.0.0.1";
				const string title = "mah title";
				const string text = "mah text";
				var newPost = new NewPost { Title = title, FullText = text, ItemID = 1, IsPlainText = true };
				var topicService = GetService();
				_forumRepo.Setup(x => x.Get(forum.ForumID)).Returns(forum);
				_forumRepo.Setup(x => x.GetForumViewRoles(forum.ForumID)).Returns(new List<string>());
				_topicRepo.Setup(t => t.GetUrlNamesThatStartWith("parsed-title")).Returns(new List<string>());
				_textParser.Setup(t => t.ClientHtmlToHtml("mah text")).Returns("html text");
				_textParser.Setup(t => t.ForumCodeToHtml("mah text")).Returns("bb text");
				_textParser.Setup(t => t.Censor("mah title")).Returns("parsed title");
				_forumPermissionService.Setup(x => x.GetPermissionContext(forum, user)).Returns(new ForumPermissionContext { UserCanModerate = false, UserCanPost = true, UserCanView = true });

				topicService.PostNewTopic(user, newPost, ip, It.IsAny<string>(), x => "", x => "");

				_postRepo.Verify(x => x.Create(It.IsAny<int>(), It.IsAny<int>(), ip, true, It.IsAny<bool>(), user.UserID, user.Name, "parsed title", "bb text", It.IsAny<DateTime>(), false, user.Name, null, false, 0));
				_topicRepo.Verify(t => t.Create(forum.ForumID, "parsed title", 0, 0, user.UserID, user.Name, user.UserID, user.Name, It.IsAny<DateTime>(), false, false, false, "parsed-title"), Times.Once());
			}

			[Fact]
			public void CallsPostRepoCreateWithHtmlText()
			{
				var forum = new Forum { ForumID = 1 };
				var user = GetUser();
				const string ip = "127.0.0.1";
				const string title = "mah title";
				const string text = "mah text";
				var newPost = new NewPost { Title = title, FullText = text, ItemID = 1, IsPlainText = false };
				var topicService = GetService();
				_forumRepo.Setup(x => x.Get(forum.ForumID)).Returns(forum);
				_forumRepo.Setup(x => x.GetForumViewRoles(forum.ForumID)).Returns(new List<string>());
				_topicRepo.Setup(t => t.GetUrlNamesThatStartWith("parsed-title")).Returns(new List<string>());
				_textParser.Setup(t => t.ClientHtmlToHtml("mah text")).Returns("html text");
				_textParser.Setup(t => t.ForumCodeToHtml("mah text")).Returns("bb text");
				_textParser.Setup(t => t.Censor("mah title")).Returns("parsed title");
				_forumPermissionService.Setup(x => x.GetPermissionContext(forum, user)).Returns(new ForumPermissionContext { UserCanModerate = false, UserCanPost = true, UserCanView = true });

				topicService.PostNewTopic(user, newPost, ip, It.IsAny<string>(), x => "", x => "");

				_postRepo.Verify(x => x.Create(It.IsAny<int>(), It.IsAny<int>(), ip, true, It.IsAny<bool>(), user.UserID, user.Name, "parsed title", "html text", It.IsAny<DateTime>(), false, user.Name, null, false, 0));
				_topicRepo.Verify(t => t.Create(forum.ForumID, "parsed title", 0, 0, user.UserID, user.Name, user.UserID, user.Name, It.IsAny<DateTime>(), false, false, false, "parsed-title"), Times.Once());
			}

			[Fact]
			public void CallsPostRepoWithTopicID()
			{
				var forum = new Forum { ForumID = 1 };
				var user = GetUser();
				const string ip = "127.0.0.1";
				const string title = "mah title";
				const string text = "mah text";
				var newPost = new NewPost { Title = title, FullText = text, ItemID = 1, IsPlainText = false };
				var topicService = GetService();
				_forumRepo.Setup(x => x.Get(forum.ForumID)).Returns(forum);
				_forumRepo.Setup(x => x.GetForumViewRoles(forum.ForumID)).Returns(new List<string>());
				_topicRepo.Setup(t => t.GetUrlNamesThatStartWith("parsed-title")).Returns(new List<string>());
				_textParser.Setup(t => t.ClientHtmlToHtml("mah text")).Returns("html text");
				_textParser.Setup(t => t.ForumCodeToHtml("mah text")).Returns("bb text");
				_textParser.Setup(t => t.Censor("mah title")).Returns("parsed title");
				_forumPermissionService.Setup(x => x.GetPermissionContext(forum, user)).Returns(new ForumPermissionContext { UserCanModerate = false, UserCanPost = true, UserCanView = true });
				_topicRepo.Setup(x => x.Create(forum.ForumID, "parsed title", 0, 0, user.UserID, user.Name, user.UserID, user.Name, It.IsAny<DateTime>(), false, false, false, "parsed-title")).Returns(543);

				topicService.PostNewTopic(user, newPost, ip, It.IsAny<string>(), x => "", x => "");

				_postRepo.Verify(x => x.Create(543, It.IsAny<int>(), ip, true, It.IsAny<bool>(), user.UserID, user.Name, "parsed title", "html text", It.IsAny<DateTime>(), false, user.Name, null, false, 0));
			}

			[Fact]
			public void DupeOfLastPostReturnsFalseIsSuccess()
			{
				var service = GetService();
				var forum = new Forum { ForumID = 1 };
				_forumRepo.Setup(x => x.Get(forum.ForumID)).Returns(forum);
				var user = GetUser();
				_forumPermissionService.Setup(x => x.GetPermissionContext(forum, user)).Returns(new ForumPermissionContext { UserCanPost = true, UserCanView = true });
				var lastPost = "last post text";
				var lastPostID = 456;
				_profileRepo.Setup(x => x.GetLastPostID(user.UserID)).Returns(lastPostID);
				_postRepo.Setup(x => x.Get(lastPostID)).Returns(new Post {FullText = lastPost, PostTime = DateTime.MinValue});
				_textParser.Setup(x => x.ClientHtmlToHtml(lastPost)).Returns(lastPost);
				_settingsManager.Setup(x => x.Current.MinimumSecondsBetweenPosts).Returns(9);

				var result = service.PostNewTopic(user, new NewPost { ItemID = forum.ForumID, FullText = lastPost }, "", "", x => "", x => "");

				Assert.False(result.IsSuccessful);
				Assert.Equal(string.Format(Resources.PostWait, 9), result.Message);
			}

			[Fact]
			public void MinimumTimeBetweenPostsNotMetReturnsFalseIsSuccess()
			{
				var service = GetService();
				var forum = new Forum { ForumID = 1 };
				_forumRepo.Setup(x => x.Get(forum.ForumID)).Returns(forum);
				var user = GetUser();
				_forumPermissionService.Setup(x => x.GetPermissionContext(forum, user)).Returns(new ForumPermissionContext { UserCanPost = true, UserCanView = true });
				var lastPost = "last post text";
				var lastPostID = 456;
				_profileRepo.Setup(x => x.GetLastPostID(user.UserID)).Returns(lastPostID);
				_postRepo.Setup(x => x.Get(lastPostID)).Returns(new Post { FullText = lastPost, PostTime = DateTime.UtcNow });
				_textParser.Setup(x => x.ClientHtmlToHtml(lastPost)).Returns(lastPost);
				_settingsManager.Setup(x => x.Current.MinimumSecondsBetweenPosts).Returns(9);

				var result = service.PostNewTopic(user, new NewPost { ItemID = forum.ForumID, FullText = "oiheorihgeorihg" }, "", "", x => "", x => "");

				Assert.False(result.IsSuccessful);
				Assert.Equal(string.Format(Resources.PostWait, 9), result.Message);
			}

			[Fact]
			public void CallsTopicRepoCreate()
			{
				var forum = new Forum { ForumID = 1 };
				var user = GetUser();
				const string ip = "127.0.0.1";
				const string title = "mah title";
				const string text = "mah text";
				var newPost = new NewPost { Title = title, FullText = text, ItemID = 1 };
				var topicService = GetService();
				_forumRepo.Setup(x => x.Get(forum.ForumID)).Returns(forum);
				_forumRepo.Setup(x => x.GetForumViewRoles(forum.ForumID)).Returns(new List<string>());
				_topicRepo.Setup(t => t.GetUrlNamesThatStartWith("parsed-title")).Returns(new List<string>());
				_textParser.Setup(t => t.ClientHtmlToHtml("mah text")).Returns("parsed text");
				_textParser.Setup(t => t.Censor("mah title")).Returns("parsed title");
				_forumPermissionService.Setup(x => x.GetPermissionContext(forum, user)).Returns(new ForumPermissionContext { UserCanModerate = false, UserCanPost = true, UserCanView = true });
				topicService.PostNewTopic(user, newPost, ip, It.IsAny<string>(), x => "", x => "");
				_topicRepo.Verify(t => t.Create(forum.ForumID, "parsed title", 0, 0, user.UserID, user.Name, user.UserID, user.Name, It.IsAny<DateTime>(), false, false, false, "parsed-title"), Times.Once());
			}

			[Fact]
			public void TitleIsParsed()
			{
				var forum = new Forum { ForumID = 1 };
				var user = GetUser();
				const string ip = "127.0.0.1";
				const string title = "mah title";
				const string text = "mah text";
				var newPost = new NewPost { Title = title, FullText = text, ItemID = 1 };
				var topicService = GetService();
				_forumRepo.Setup(x => x.Get(forum.ForumID)).Returns(forum);
				_forumRepo.Setup(x => x.GetForumViewRoles(forum.ForumID)).Returns(new List<string>());
				_topicRepo.Setup(t => t.GetUrlNamesThatStartWith("parsed-title")).Returns(new List<string>());
				_textParser.Setup(t => t.ClientHtmlToHtml("mah text")).Returns("parsed text");
				_textParser.Setup(t => t.Censor("mah title")).Returns("parsed title");
				_forumPermissionService.Setup(x => x.GetPermissionContext(forum, user)).Returns(new ForumPermissionContext { UserCanModerate = false, UserCanPost = true, UserCanView = true });

				topicService.PostNewTopic(user, newPost, ip, It.IsAny<string>(), x => "", x => "");

				_topicRepo.Verify(t => t.Create(forum.ForumID, "parsed title", 0, 0, user.UserID, user.Name, user.UserID, user.Name, It.IsAny<DateTime>(), false, false, false, "parsed-title"), Times.Once());
			}

			[Fact]
			public void CallsForumTopicPostIncrement()
			{
				DoUpNewTopic();
				_forumRepo.Verify(f => f.IncrementPostAndTopicCount(1), Times.Once());
			}

			[Fact]
			public void CallsForumUpdateLastUser()
			{
				var user = DoUpNewTopic();
				_forumRepo.Verify(f => f.UpdateLastTimeAndUser(1, It.IsAny<DateTime>(), user.Name), Times.Once());
			}

			[Fact]
			public void CallsProfileSetLastPost()
			{
				var user = DoUpNewTopic();
				_profileRepo.Verify(p => p.SetLastPostID(user.UserID, 69), Times.Once());
			}

			[Fact]
			public void PublishesNewTopicEvent()
			{
				var user = DoUpNewTopic();
				_eventPublisher.Verify(x => x.ProcessEvent(It.IsAny<string>(), user, EventDefinitionService.StaticEventIDs.NewTopic, false), Times.Once());
			}

			[Fact]
			public void PublishesNewPostEvent()
			{
				var user = DoUpNewTopic();
				_eventPublisher.Verify(x => x.ProcessEvent(String.Empty, user, EventDefinitionService.StaticEventIDs.NewPost, true), Times.Once());
			}

			[Fact]
			public void CallsBroker()
			{
				DoUpNewTopic();
				_broker.Verify(x => x.NotifyForumUpdate(It.IsAny<Forum>()), Times.Once());
				_broker.Verify(x => x.NotifyTopicUpdate(It.IsAny<Topic>(), It.IsAny<Forum>(), It.IsAny<string>()), Times.Once());
			}

			[Fact]
			public void QueuesTopicForIndexing()
			{
				DoUpNewTopic();
				_tenantService.Setup(x => x.GetTenant()).Returns("");
				_searchIndexQueueRepo.Verify(x => x.Enqueue(It.IsAny<SearchIndexPayload>()), Times.Once);
			}

			[Fact]
			public void DoesNotPublishToFeedIfForumHasViewRestrictions()
			{
				var forum = new Forum { ForumID = 1 };
				var user = GetUser();
				const string ip = "127.0.0.1";
				const string title = "mah title";
				const string text = "mah text";
				var newPost = new NewPost { Title = title, FullText = text, ItemID = 1 };
				var topicService = GetService();
				_forumRepo.Setup(x => x.Get(forum.ForumID)).Returns(forum);
				_forumRepo.Setup(x => x.GetForumViewRoles(forum.ForumID)).Returns(new List<string> { "Admin" });
				_topicRepo.Setup(t => t.GetUrlNamesThatStartWith("parsed-title")).Returns(new List<string>());
				_textParser.Setup(t => t.ClientHtmlToHtml("mah text")).Returns("parsed text");
				_textParser.Setup(t => t.Censor("mah title")).Returns("parsed title");
				_topicRepo.Setup(t => t.Create(forum.ForumID, "parsed title", 0, 0, user.UserID, user.Name, user.UserID, user.Name, It.IsAny<DateTime>(), false, false, false, "parsed-title")).Returns(2);
				_forumPermissionService.Setup(x => x.GetPermissionContext(forum, user)).Returns(new ForumPermissionContext { UserCanModerate = false, UserCanPost = true, UserCanView = true });
				topicService.PostNewTopic(user, newPost, ip, It.IsAny<string>(), x => "", x => "");
				_eventPublisher.Verify(x => x.ProcessEvent(It.IsAny<string>(), It.IsAny<User>(), EventDefinitionService.StaticEventIDs.NewTopic, true), Times.Once());
			}

			[Fact]
			public void ReturnsTopic()
			{
				var forum = new Forum {ForumID = 1};
				var user = GetUser();
				const string ip = "127.0.0.1";
				const string title = "mah title";
				const string text = "mah text";
				var newPost = new NewPost {Title = title, FullText = text, ItemID = 1};
				var topicService = GetService();
				_forumRepo.Setup(x => x.Get(forum.ForumID)).Returns(forum);
				_forumRepo.Setup(x => x.GetForumViewRoles(forum.ForumID)).Returns(new List<string>());
				_topicRepo.Setup(t => t.GetUrlNamesThatStartWith("parsed-title")).Returns(new List<string>());
				_textParser.Setup(t => t.ClientHtmlToHtml("mah text")).Returns("parsed text");
				_textParser.Setup(t => t.Censor("mah title")).Returns("parsed title");
				_topicRepo.Setup(t => t.Create(forum.ForumID, "parsed title", 0, 0, user.UserID, user.Name, user.UserID, user.Name, It.IsAny<DateTime>(), false, false, false, "parsed-title")).Returns(2);
				_forumPermissionService.Setup(x => x.GetPermissionContext(forum, user)).Returns(new ForumPermissionContext { UserCanModerate = false, UserCanPost = true, UserCanView = true });
				var result = topicService.PostNewTopic(user, newPost, ip, It.IsAny<string>(), x => "", x => "");
				Assert.Equal(2, result.Data.TopicID);
				Assert.Equal(forum.ForumID, result.Data.ForumID);
				Assert.Equal("parsed title", result.Data.Title);
				Assert.Equal(0, result.Data.ReplyCount);
				Assert.Equal(0, result.Data.ViewCount);
				Assert.Equal(user.UserID, result.Data.StartedByUserID);
				Assert.Equal(user.Name, result.Data.StartedByName);
				Assert.Equal(user.UserID, result.Data.LastPostUserID);
				Assert.Equal(user.Name, result.Data.LastPostName);
				Assert.False(result.Data.IsClosed);
				Assert.False(result.Data.IsDeleted);
				Assert.False(result.Data.IsPinned);
				Assert.Equal("parsed-title", result.Data.UrlName);
			}
		}

		public class PostReplyTests : PostNewTopicTests
		{
			[Fact]
			public void NoUserReturnsFalseIsSuccessful()
			{
				var service = GetService();

				var result = service.PostReply(null, 0, "", false, new NewPost(), DateTime.MaxValue, x => "", (u, t) => "", "", x => "", x => "");

				Assert.False(result.IsSuccessful);
			}

			[Fact]
			public void NoTopicReturnsFalseIsSuccessful()
			{
				var service = GetService();
				_topicRepo.Setup(x => x.Get(It.IsAny<int>())).Returns((Topic) null);

				var result = service.PostReply(GetUser(), 0, "", false, new NewPost(), DateTime.MaxValue, x => "", (u, t) => "", "", x => "", x => "");

				Assert.False(result.IsSuccessful);
				Assert.Equal(Resources.TopicNotExist, result.Message);
			}

			[Fact]
			public void NoForumThrows()
			{
				var service = GetService();
				_topicRepo.Setup(x => x.Get(It.IsAny<int>())).Returns(new Topic());
				_forumRepo.Setup(x => x.Get(It.IsAny<int>())).Returns((Forum) null);

				Assert.Throws<Exception>(() => service.PostReply(GetUser(), 0, "", false, new NewPost(), DateTime.MaxValue, x => "", (u, t) => "", "", x => "", x => ""));
			}

			[Fact]
			public void NoViewPermissionReturnsFalseIsSuccessful()
			{
				var service = GetService();
				var user = GetUser();
				var forum = new Forum {ForumID = 1};
				var topic = new Topic {ForumID = forum.ForumID};
				var newPost = new NewPost {ItemID = topic.TopicID};
				_topicRepo.Setup(x => x.Get(It.IsAny<int>())).Returns(topic);
				_forumRepo.Setup(x => x.Get(forum.ForumID)).Returns(forum);
				_forumPermissionService.Setup(x => x.GetPermissionContext(forum, user)).Returns(new ForumPermissionContext {UserCanView = false, UserCanPost = true});

				var result = service.PostReply(user, 0, "", false, newPost, DateTime.MaxValue, x => "", (u, t) => "", "", x => "", x => "");

				Assert.False(result.IsSuccessful);
				Assert.Equal(Resources.ForumNoView, result.Message);
			}

			[Fact]
			public void NoPostPermissionReturnsFalseIsSuccessful()
			{
				var service = GetService();
				var user = GetUser();
				var forum = new Forum { ForumID = 1 };
				var topic = new Topic { ForumID = forum.ForumID };
				var newPost = new NewPost { ItemID = topic.TopicID };
				_topicRepo.Setup(x => x.Get(It.IsAny<int>())).Returns(topic);
				_forumRepo.Setup(x => x.Get(forum.ForumID)).Returns(forum);
				_forumPermissionService.Setup(x => x.GetPermissionContext(forum, user)).Returns(new ForumPermissionContext { UserCanView = true, UserCanPost = false });

				var result = service.PostReply(user, 0, "", false, newPost, DateTime.MaxValue, x => "", (u, t) => "", "", x => "", x => "");

				Assert.False(result.IsSuccessful);
				Assert.Equal(Resources.ForumNoPost, result.Message);
			}

			[Fact]
			public void ClosedTopicReturnsFalseIsSuccessful()
			{
				var service = GetService();
				_topicRepo.Setup(x => x.Get(It.IsAny<int>())).Returns(new Topic{IsClosed = true});

				var result = service.PostReply(GetUser(), 0, "", false, new NewPost(), DateTime.MaxValue, x => "", (u, t) => "", "", x => "", x => "");

				Assert.False(result.IsSuccessful);
				Assert.Equal(Resources.Closed, result.Message);
			}

			[Fact]
			public void UsesPlainTextParsed()
			{
				var topic = new Topic { TopicID = 1, Title = "" };
				var user = GetUser();
				var postTime = DateTime.UtcNow;
				var service = GetService();
				var forum = new Forum { ForumID = topic.ForumID };
				_topicRepo.Setup(x => x.Get(topic.TopicID)).Returns(topic);
				_forumPermissionService.Setup(x => x.GetPermissionContext(forum, user)).Returns(new ForumPermissionContext { UserCanPost = true, UserCanView = true });
				_textParser.Setup(x => x.ForumCodeToHtml(It.IsAny<string>())).Returns("parsed text");
				_forumRepo.Setup(x => x.Get(forum.ForumID)).Returns(forum);
				_forumRepo.Setup(x => x.GetForumViewRoles(It.IsAny<int>())).Returns(new List<string>());
				var newPost = new NewPost { FullText = "mah text", Title = "mah title", IncludeSignature = true, ItemID = topic.TopicID, IsPlainText = true };
				_textParser.Setup(t => t.Censor(newPost.Title)).Returns("parsed title");
				service.PostReply(user, 0, "127.0.0.1", false, newPost, postTime, x => "", (t, p) => "", "", x => "", x => "");
				_postRepo.Verify(p => p.Create(topic.TopicID, 0, "127.0.0.1", false, true, user.UserID, user.Name, "parsed title", "parsed text", postTime, false, user.Name, null, false, 0));
			}

			[Fact]
			public void UsesRichTextParsed()
			{
				var topic = new Topic { TopicID = 1, Title = "" };
				var user = GetUser();
				var postTime = DateTime.UtcNow;
				var service = GetService();
				var forum = new Forum { ForumID = topic.ForumID };
				_topicRepo.Setup(x => x.Get(topic.TopicID)).Returns(topic);
				_forumPermissionService.Setup(x => x.GetPermissionContext(forum, user)).Returns(new ForumPermissionContext { UserCanPost = true, UserCanView = true });
				_textParser.Setup(x => x.ClientHtmlToHtml(It.IsAny<string>())).Returns("parsed text");
				_forumRepo.Setup(x => x.Get(forum.ForumID)).Returns(forum);
				_forumRepo.Setup(x => x.GetForumViewRoles(It.IsAny<int>())).Returns(new List<string>());
				var newPost = new NewPost { FullText = "mah text", Title = "mah title", IncludeSignature = true, ItemID = topic.TopicID, IsPlainText = false };
				_textParser.Setup(t => t.Censor(newPost.Title)).Returns("parsed title");
				service.PostReply(user, 0, "127.0.0.1", false, newPost, postTime, x => "", (t, p) => "", "", x => "", x => "");
				_postRepo.Verify(p => p.Create(topic.TopicID, 0, "127.0.0.1", false, true, user.UserID, user.Name, "parsed title", "parsed text", postTime, false, user.Name, null, false, 0));
			}

			[Fact]
			public void DupeOfLastPostFails()
			{
				var topic = new Topic { TopicID = 1, Title = "" };
				var user = GetUser();
				var postTime = DateTime.UtcNow;
				var service = GetService();
				var forum = new Forum { ForumID = topic.ForumID };
				_topicRepo.Setup(x => x.Get(topic.TopicID)).Returns(topic);
				_forumPermissionService.Setup(x => x.GetPermissionContext(forum, user)).Returns(new ForumPermissionContext { UserCanPost = true, UserCanView = true });
				_textParser.Setup(x => x.ClientHtmlToHtml(It.IsAny<string>())).Returns("parsed text");
				_forumRepo.Setup(x => x.Get(forum.ForumID)).Returns(forum);
				_forumRepo.Setup(x => x.GetForumViewRoles(It.IsAny<int>())).Returns(new List<string>());
				_profileRepo.Setup(x => x.GetLastPostID(user.UserID)).Returns(654);
				_postRepo.Setup(x => x.Get(654)).Returns(new Post {FullText = "parsed text", PostTime = DateTime.MinValue});
				_settingsManager.Setup(x => x.Current.MinimumSecondsBetweenPosts).Returns(9);
				var newPost = new NewPost { FullText = "mah text", Title = "mah title", IncludeSignature = true, ItemID = topic.TopicID, IsPlainText = false };

				var result = service.PostReply(user, 0, "127.0.0.1", false, newPost, postTime, x => "", (t, p) => "", "", x => "", x => "");

				Assert.False(result.IsSuccessful);
				Assert.Equal(string.Format(Resources.PostWait, 9), result.Message);
			}

			[Fact]
			public void MinTimeSinceLastPostTooShortFails()
			{
				var topic = new Topic { TopicID = 1, Title = "" };
				var user = GetUser();
				var postTime = DateTime.UtcNow;
				var service = GetService();
				var forum = new Forum { ForumID = topic.ForumID };
				_topicRepo.Setup(x => x.Get(topic.TopicID)).Returns(topic);
				_forumPermissionService.Setup(x => x.GetPermissionContext(forum, user)).Returns(new ForumPermissionContext { UserCanPost = true, UserCanView = true });
				_textParser.Setup(x => x.ClientHtmlToHtml(It.IsAny<string>())).Returns("oihf text");
				_forumRepo.Setup(x => x.Get(forum.ForumID)).Returns(forum);
				_forumRepo.Setup(x => x.GetForumViewRoles(It.IsAny<int>())).Returns(new List<string>());
				_profileRepo.Setup(x => x.GetLastPostID(user.UserID)).Returns(654);
				_postRepo.Setup(x => x.Get(654)).Returns(new Post { FullText = "parsed text", PostTime = DateTime.UtcNow });
				_settingsManager.Setup(x => x.Current.MinimumSecondsBetweenPosts).Returns(9);
				var newPost = new NewPost { FullText = "mah text", Title = "mah title", IncludeSignature = true, ItemID = topic.TopicID, IsPlainText = false };

				var result = service.PostReply(user, 0, "127.0.0.1", false, newPost, postTime, x => "", (t, p) => "", "", x => "", x => "");

				Assert.False(result.IsSuccessful);
				Assert.Equal(string.Format(Resources.PostWait, 9), result.Message);
			}

			[Fact]
			public void EmptyPostFails()
			{
				var topic = new Topic { TopicID = 1, Title = "" };
				var user = GetUser();
				var postTime = DateTime.UtcNow;
				var service = GetService();
				var forum = new Forum { ForumID = topic.ForumID };
				_topicRepo.Setup(x => x.Get(topic.TopicID)).Returns(topic);
				_forumPermissionService.Setup(x => x.GetPermissionContext(forum, user)).Returns(new ForumPermissionContext { UserCanPost = true, UserCanView = true });
				_textParser.Setup(x => x.ClientHtmlToHtml(It.IsAny<string>())).Returns("");
				_forumRepo.Setup(x => x.Get(forum.ForumID)).Returns(forum);
				_forumRepo.Setup(x => x.GetForumViewRoles(It.IsAny<int>())).Returns(new List<string>());
				_profileRepo.Setup(x => x.GetLastPostID(user.UserID)).Returns(654);
				_settingsManager.Setup(x => x.Current.MinimumSecondsBetweenPosts).Returns(9);
				var newPost = new NewPost { FullText = "mah text", Title = "mah title", IncludeSignature = true, ItemID = topic.TopicID, IsPlainText = false };

				var result = service.PostReply(user, 0, "127.0.0.1", false, newPost, postTime, x => "", (t, p) => "", "", x => "", x => "");

				Assert.False(result.IsSuccessful);
				Assert.Equal(Resources.PostEmpty, result.Message);
			}

			[Fact]
			public void HitsRepo()
			{
				var topic = new Topic { TopicID = 1, Title = "" };
				var user = GetUser();
				var postTime = DateTime.UtcNow;
				var service = GetService();
				var forum = new Forum { ForumID = topic.ForumID };
				_topicRepo.Setup(x => x.Get(topic.TopicID)).Returns(topic);
				_forumPermissionService.Setup(x => x.GetPermissionContext(forum, user)).Returns(new ForumPermissionContext { UserCanPost = true, UserCanView = true });
				_textParser.Setup(x => x.ClientHtmlToHtml(It.IsAny<string>())).Returns("parsed text");
				_forumRepo.Setup(x => x.Get(forum.ForumID)).Returns(forum);
				_forumRepo.Setup(x => x.GetForumViewRoles(It.IsAny<int>())).Returns(new List<string>());
				var newPost = new NewPost { FullText = "mah text", Title = "mah title", IncludeSignature = true, ItemID = topic.TopicID };
				_textParser.Setup(t => t.Censor(newPost.Title)).Returns("parsed title");
				service.PostReply(user, 0, "127.0.0.1", false, newPost, postTime, x => "", (t,p) => "", "", x => "", x => "");
				_postRepo.Verify(p => p.Create(topic.TopicID, 0, "127.0.0.1", false, true, user.UserID, user.Name, "parsed title", "parsed text", postTime, false, user.Name, null, false, 0));
			}

			[Fact]
			public void HitsSubscribedService()
			{
				var topic = new Topic { TopicID = 1 };
				var user = GetUser();
				var postTime = DateTime.UtcNow;
				var service = GetService();
				var forum = new Forum { ForumID = topic.ForumID };
				_topicRepo.Setup(x => x.Get(topic.TopicID)).Returns(topic);
				_forumPermissionService.Setup(x => x.GetPermissionContext(forum, user)).Returns(new ForumPermissionContext { UserCanPost = true, UserCanView = true });
				_textParser.Setup(x => x.ClientHtmlToHtml(It.IsAny<string>())).Returns("parsed text");
				_forumRepo.Setup(x => x.Get(forum.ForumID)).Returns(forum);
				_forumRepo.Setup(x => x.GetForumViewRoles(It.IsAny<int>())).Returns(new List<string>());
				var newPost = new NewPost { FullText = "mah text", Title = "mah title", IncludeSignature = true, ItemID = topic.TopicID };
				service.PostReply(user, 0, "127.0.0.1", false, newPost, postTime, x => "", (t,p) => "", "", x => "", x => "");
				_subscribedTopicsService.Verify(s => s.NotifySubscribers(topic, user, It.IsAny<string>(), It.IsAny<Func<User, Topic, string>>()), Times.Once());
			}

			[Fact]
			public void IncrementsTopicReplyCount()
			{
				var topic = new Topic { TopicID = 1 };
				var user = GetUser();
				var postTime = DateTime.UtcNow;
				var service = GetService();
				var forum = new Forum { ForumID = topic.ForumID };
				_topicRepo.Setup(x => x.Get(topic.TopicID)).Returns(topic);
				_forumPermissionService.Setup(x => x.GetPermissionContext(forum, user)).Returns(new ForumPermissionContext { UserCanPost = true, UserCanView = true });
				_textParser.Setup(x => x.ClientHtmlToHtml(It.IsAny<string>())).Returns("parsed text");
				_forumRepo.Setup(x => x.Get(forum.ForumID)).Returns(forum);
				_forumRepo.Setup(x => x.GetForumViewRoles(It.IsAny<int>())).Returns(new List<string>());
				var newPost = new NewPost { FullText = "mah text", Title = "mah title", IncludeSignature = true, ItemID = topic.TopicID };
				service.PostReply(user, 0, "127.0.0.1", false, newPost, postTime, x => "", (t,p) => "", "", x => "", x => "");
				_topicRepo.Verify(t => t.IncrementReplyCount(1));
			}

			[Fact]
			public void IncrementsForumPostCount()
			{
				var topic = new Topic { TopicID = 1, ForumID = 2 };
				var user = GetUser();
				var postTime = DateTime.UtcNow;
				var service = GetService();
				var forum = new Forum { ForumID = topic.ForumID };
				_topicRepo.Setup(x => x.Get(topic.TopicID)).Returns(topic);
				_forumPermissionService.Setup(x => x.GetPermissionContext(forum, user)).Returns(new ForumPermissionContext { UserCanPost = true, UserCanView = true });
				_textParser.Setup(x => x.ClientHtmlToHtml(It.IsAny<string>())).Returns("parsed text");
				_forumRepo.Setup(x => x.Get(forum.ForumID)).Returns(forum);
				_forumRepo.Setup(x => x.GetForumViewRoles(It.IsAny<int>())).Returns(new List<string>());
				var newPost = new NewPost { FullText = "mah text", Title = "mah title", IncludeSignature = true, ItemID = topic.TopicID };
				service.PostReply(user, 0, "127.0.0.1", false, newPost, postTime, x => "", (t, p) => "", "", x => "", x => "");
				_forumRepo.Verify(f => f.IncrementPostCount(2));
			}

			[Fact]
			public void UpdatesTopicLastInfo()
			{
				var topic = new Topic { TopicID = 1, ForumID = 2 };
				var user = GetUser();
				var postTime = DateTime.UtcNow;
				var service = GetService();
				var forum = new Forum { ForumID = topic.ForumID };
				_topicRepo.Setup(x => x.Get(topic.TopicID)).Returns(topic);
				_forumPermissionService.Setup(x => x.GetPermissionContext(forum, user)).Returns(new ForumPermissionContext { UserCanPost = true, UserCanView = true });
				_textParser.Setup(x => x.ClientHtmlToHtml(It.IsAny<string>())).Returns("parsed text");
				_forumRepo.Setup(x => x.Get(forum.ForumID)).Returns(forum);
				_forumRepo.Setup(x => x.GetForumViewRoles(It.IsAny<int>())).Returns(new List<string>());
				var newPost = new NewPost { FullText = "mah text", Title = "mah title", IncludeSignature = true, ItemID = topic.TopicID };
				service.PostReply(user, 0, "127.0.0.1", false, newPost, postTime, x => "", (t, p) => "", "", x => "", x => "");
				_topicRepo.Verify(t => t.UpdateLastTimeAndUser(topic.TopicID, user.UserID, user.Name, postTime));
			}

			[Fact]
			public void UpdatesForumLastInfo()
			{
				var topic = new Topic { TopicID = 1, ForumID = 2 };
				var user = GetUser();
				var postTime = DateTime.UtcNow;
				var service = GetService();
				var forum = new Forum { ForumID = topic.ForumID };
				_topicRepo.Setup(x => x.Get(topic.TopicID)).Returns(topic);
				_forumPermissionService.Setup(x => x.GetPermissionContext(forum, user)).Returns(new ForumPermissionContext { UserCanPost = true, UserCanView = true });
				_textParser.Setup(x => x.ClientHtmlToHtml(It.IsAny<string>())).Returns("parsed text");
				_forumRepo.Setup(x => x.Get(forum.ForumID)).Returns(forum);
				_forumRepo.Setup(x => x.GetForumViewRoles(It.IsAny<int>())).Returns(new List<string>());
				var newPost = new NewPost { FullText = "mah text", Title = "mah title", IncludeSignature = true, ItemID = topic.TopicID };
				service.PostReply(user, 0, "127.0.0.1", false, newPost, postTime, x => "", (t, p) => "", "", x => "", x => "");
				_forumRepo.Verify(f => f.UpdateLastTimeAndUser(topic.ForumID, postTime, user.Name));
			}

			[Fact]
			public void PostQueuesMarksTopicForIndexing()
			{
				var topic = new Topic { TopicID = 1, ForumID = 2 };
				var user = GetUser();
				var postTime = DateTime.UtcNow;
				var service = GetService();
				var forum = new Forum {ForumID = topic.ForumID};
				_forumPermissionService.Setup(x => x.GetPermissionContext(forum, user)).Returns(new ForumPermissionContext {UserCanPost = true, UserCanView = true});
				_textParser.Setup(x => x.ClientHtmlToHtml(It.IsAny<string>())).Returns("parsed text");
				_forumRepo.Setup(x => x.Get(forum.ForumID)).Returns(forum);
				_forumRepo.Setup(x => x.GetForumViewRoles(It.IsAny<int>())).Returns(new List<string>());
				_tenantService.Setup(x => x.GetTenant()).Returns("");
				_topicRepo.Setup(x => x.Get(topic.TopicID)).Returns(topic);
				var newPost = new NewPost { FullText = "mah text", Title = "mah title", IncludeSignature = true, ItemID = topic.TopicID };
				service.PostReply(user, 0, "127.0.0.1", false, newPost, postTime, x => "", (t, p) => "", "", x => "", x => "");
				_searchIndexQueueRepo.Verify(x => x.Enqueue(It.IsAny<SearchIndexPayload>()), Times.Once);
			}

			[Fact]
			public void NotifiesBroker()
			{
				var topic = new Topic { TopicID = 1, ForumID = 2 };
				var user = GetUser();
				var postTime = DateTime.UtcNow;
				var service = GetService();
				_forumRepo.Setup(x => x.GetForumViewRoles(It.IsAny<int>())).Returns(new List<string>());
				var newPost = new NewPost { FullText = "mah text", Title = "mah title", IncludeSignature = true, ItemID = topic.TopicID };
				var forum = new Forum { ForumID = topic.ForumID };
				_topicRepo.Setup(x => x.Get(topic.TopicID)).Returns(topic);
				_forumPermissionService.Setup(x => x.GetPermissionContext(forum, user)).Returns(new ForumPermissionContext { UserCanPost = true, UserCanView = true });
				_textParser.Setup(x => x.ClientHtmlToHtml(It.IsAny<string>())).Returns("parsed text");
				_forumRepo.Setup(x => x.Get(forum.ForumID)).Returns(forum);
				_forumRepo.Setup(x => x.Get(topic.ForumID)).Returns(forum);
				_topicRepo.Setup(x => x.Get(topic.TopicID)).Returns(topic);
				service.PostReply(user, 0, "127.0.0.1", false, newPost, postTime, x => "", (t, p) => "", "", x => "", x => "");
				_broker.Verify(x => x.NotifyForumUpdate(forum), Times.Once());
				_broker.Verify(x => x.NotifyTopicUpdate(topic, forum, It.IsAny<string>()), Times.Once());
				_broker.Verify(x => x.NotifyNewPost(topic, It.IsAny<int>()), Times.Once());
			}

			[Fact]
			public void SetsProfileLastPostID()
			{
				var topic = new Topic { TopicID = 1, ForumID = 2 };
				var user = GetUser();
				var postTime = DateTime.UtcNow;
				var service = GetService();
				var forum = new Forum { ForumID = topic.ForumID };
				_topicRepo.Setup(x => x.Get(topic.TopicID)).Returns(topic);
				_forumPermissionService.Setup(x => x.GetPermissionContext(forum, user)).Returns(new ForumPermissionContext { UserCanPost = true, UserCanView = true });
				_textParser.Setup(x => x.ClientHtmlToHtml(It.IsAny<string>())).Returns("parsed text");
				_forumRepo.Setup(x => x.Get(forum.ForumID)).Returns(forum);
				_forumRepo.Setup(x => x.GetForumViewRoles(It.IsAny<int>())).Returns(new List<string>());
				var newPost = new NewPost { FullText = "mah text", Title = "mah title", IncludeSignature = true, ItemID = topic.TopicID };
				var result = service.PostReply(user, 0, "127.0.0.1", false, newPost, postTime, x => "", (t, p) => "", "", x => "", x => "");
				_profileRepo.Verify(p => p.SetLastPostID(user.UserID, result.Data.PostID), Times.Once);
			}

			[Fact]
			public void PublishesEvent()
			{
				var topic = new Topic { TopicID = 1, ForumID = 2 };
				var user = GetUser();
				var postTime = DateTime.UtcNow;
				var service = GetService();
				var forum = new Forum { ForumID = topic.ForumID };
				_topicRepo.Setup(x => x.Get(topic.TopicID)).Returns(topic);
				_forumPermissionService.Setup(x => x.GetPermissionContext(forum, user)).Returns(new ForumPermissionContext { UserCanPost = true, UserCanView = true });
				_textParser.Setup(x => x.ClientHtmlToHtml(It.IsAny<string>())).Returns("parsed text");
				_forumRepo.Setup(x => x.Get(forum.ForumID)).Returns(forum);
				_forumRepo.Setup(x => x.GetForumViewRoles(It.IsAny<int>())).Returns(new List<string>());
				var newPost = new NewPost { FullText = "mah text", Title = "mah title", IncludeSignature = true, ItemID = topic.TopicID };
				service.PostReply(user, 0, "127.0.0.1", false, newPost, postTime, x => "", (t, p) => "", "", x => "", x => "");
				_eventPublisher.Verify(x => x.ProcessEvent(It.IsAny<string>(), user, EventDefinitionService.StaticEventIDs.NewPost, false), Times.Once());
			}

			[Fact]
			public void DoesNotPublisheEventOnViewRestrictedForum()
			{
				var topic = new Topic { TopicID = 1, ForumID = 2 };
				var user = GetUser();
				var postTime = DateTime.UtcNow;
				var service = GetService();
				var forum = new Forum {ForumID = topic.ForumID};
				_topicRepo.Setup(x => x.Get(topic.TopicID)).Returns(topic);
				_forumPermissionService.Setup(x => x.GetPermissionContext(forum, user)).Returns(new ForumPermissionContext { UserCanPost = true, UserCanView = true });
				_textParser.Setup(x => x.ClientHtmlToHtml(It.IsAny<string>())).Returns("parsed text");
				_forumRepo.Setup(x => x.Get(forum.ForumID)).Returns(forum);
				_forumRepo.Setup(x => x.GetForumViewRoles(It.IsAny<int>())).Returns(new List<string> { "Admin" });
				var newPost = new NewPost { FullText = "mah text", Title = "mah title", IncludeSignature = true, ItemID = topic.TopicID };
				service.PostReply(user, 0, "127.0.0.1", false, newPost, postTime, x => "", (t, p) => "", "", x => "", x => "");
				_eventPublisher.Verify(x => x.ProcessEvent(It.IsAny<string>(), user, EventDefinitionService.StaticEventIDs.NewPost, true), Times.Once());
			}

			[Fact]
			public void ReturnsHydratedObject()
			{
				var topic = new Topic { TopicID = 1 };
				var user = GetUser();
				var postTime = DateTime.UtcNow;
				var service = GetService();
				_forumRepo.Setup(x => x.GetForumViewRoles(It.IsAny<int>())).Returns(new List<string>());
				_postRepo.Setup(p => p.Create(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), false, true, It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), false, It.IsAny<string>(), null, false, 0)).Returns(123);
				var forum = new Forum { ForumID = topic.ForumID };
				_topicRepo.Setup(x => x.Get(topic.TopicID)).Returns(topic);
				_forumPermissionService.Setup(x => x.GetPermissionContext(forum, user)).Returns(new ForumPermissionContext { UserCanPost = true, UserCanView = true });
				_textParser.Setup(x => x.ClientHtmlToHtml(It.IsAny<string>())).Returns("parsed text");
				_forumRepo.Setup(x => x.Get(forum.ForumID)).Returns(forum);
				_textParser.Setup(t => t.Censor("mah title")).Returns("parsed title");
				var newPost = new NewPost { FullText = "mah text", Title = "mah title", IncludeSignature = true, ItemID = topic.TopicID };
				var result = service.PostReply(user, 0, "127.0.0.1", false, newPost, postTime, x => "", (t, p) => "", "", x => "", x => "");
				Assert.Equal(topic.TopicID, result.Data.TopicID);
				Assert.Equal("parsed text", result.Data.FullText);
				Assert.Equal("127.0.0.1", result.Data.IP);
				Assert.False(result.Data.IsDeleted);
				Assert.False(result.Data.IsEdited);
				Assert.False(result.Data.IsFirstInTopic);
				Assert.Equal(user.Name, result.Data.LastEditName);
				Assert.Null(result.Data.LastEditTime);
				Assert.Equal(user.Name, result.Data.Name);
				Assert.Equal(0, result.Data.ParentPostID);
				Assert.Equal(123, result.Data.PostID);
				Assert.Equal(postTime, result.Data.PostTime);
				Assert.True(result.Data.ShowSig);
				Assert.Equal("parsed title", result.Data.Title);
				Assert.Equal(user.UserID, result.Data.UserID);
			}
		}

		public class EditPostTests : PostMasterServiceTests
		{
			[Fact]
			public void EditPostCensorsTitle()
			{
				var service = GetService();
				service.EditPost(new Post { PostID = 456 }, new PostEdit { Title = "blah" }, new User());
				_textParser.Verify(t => t.Censor("blah"), Times.Exactly(1));
			}

			[Fact]
			public void EditPostPlainTextParsed()
			{
				var service = GetService();
				service.EditPost(new Post { PostID = 456 }, new PostEdit { FullText = "blah", IsPlainText = true }, new User());
				_textParser.Verify(t => t.ForumCodeToHtml("blah"), Times.Exactly(1));
			}

			[Fact]
			public void EditPostRichTextParsed()
			{
				var service = GetService();
				service.EditPost(new Post { PostID = 456 }, new PostEdit { FullText = "blah", IsPlainText = false }, new User());
				_textParser.Verify(t => t.ClientHtmlToHtml("blah"), Times.Exactly(1));
			}

			[Fact]
			public void EditPostSavesMappedValues()
			{
				var service = GetService();
				var post = new Post { PostID = 67 };
				_postRepo.Setup(p => p.Update(It.IsAny<Post>())).Callback<Post>(p => post = p);
				_textParser.Setup(t => t.ClientHtmlToHtml("blah")).Returns("new");
				_textParser.Setup(t => t.Censor("unparsed title")).Returns("new title");
				service.EditPost(new Post { PostID = 456, ShowSig = false }, new PostEdit { FullText = "blah", Title = "unparsed title", IsPlainText = false, ShowSig = true }, new User { UserID = 123, Name = "dude" });
				Assert.NotEqual(post.LastEditTime, new DateTime(2009, 1, 1));
				Assert.Equal(456, post.PostID);
				Assert.Equal("new", post.FullText);
				Assert.Equal("new title", post.Title);
				Assert.True(post.ShowSig);
				Assert.True(post.IsEdited);
				Assert.Equal("dude", post.LastEditName);
			}

			[Fact]
			public void EditPostModeratorLogged()
			{
				var service = GetService();
				var user = new User { UserID = 123, Name = "dude" };
				_textParser.Setup(t => t.ClientHtmlToHtml("blah")).Returns("new");
				_textParser.Setup(t => t.Censor("unparsed title")).Returns("new title");
				service.EditPost(new Post { PostID = 456, ShowSig = false, FullText = "old text" }, new PostEdit { FullText = "blah", Title = "unparsed title", IsPlainText = false, ShowSig = true, Comment = "mah comment" }, user);
				_moderationLogService.Verify(m => m.LogPost(user, ModerationType.PostEdit, It.IsAny<Post>(), "mah comment", "old text"), Times.Exactly(1));
			}

			[Fact]
			public void EditPostQueuesTopicForIndexing()
			{
				var service = GetService();
				var user = new User { UserID = 123, Name = "dude" };
				var post = new Post { PostID = 456, ShowSig = false, FullText = "old text", TopicID = 999 };
				_tenantService.Setup(x => x.GetTenant()).Returns("");

				service.EditPost(post, new PostEdit { FullText = "blah", Title = "unparsed title", IsPlainText = false, ShowSig = true, Comment = "mah comment" }, user);

				_searchIndexQueueRepo.Verify(x => x.Enqueue(It.IsAny<SearchIndexPayload>()), Times.Once);
			}
		}
	}
}