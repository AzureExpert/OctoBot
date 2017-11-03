﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using Autofac;
using GitHubBot.Dialogs;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Internals;
using Microsoft.Bot.Builder.Luis;
using Microsoft.Bot.Connector;
using Activity = Microsoft.Bot.Connector.Activity;

namespace GitHubBot
{
	[BotAuthentication]
	public class MessagesController : ApiController
	{
		/// <summary>
		/// POST: api/Messages
		/// Receive a message from a user and reply to it
		/// </summary>
		public async Task<HttpResponseMessage> Post([FromBody]Activity activity)
		{
			switch (activity.GetActivityType())
			{
				// all messages pass through one dialog for now
				case ActivityTypes.Message:
					LuisModelAttribute attr = new LuisModelAttribute(ConfigurationManager.AppSettings[Constants.LuisModelIdKey], ConfigurationManager.AppSettings[Constants.LuisSubscriptionKey]);
					LuisService luisSvc = new LuisService(attr);
					await Conversation.SendAsync(activity, () => new GitHubLuisDialog(luisSvc));
					break;

				// send a "hello" to someone who just joined the conversation (not all channels support this)
				case ActivityTypes.ConversationUpdate:
					IConversationUpdateActivity update = activity;
					using (ILifetimeScope scope = DialogModule.BeginLifetimeScope(Conversation.Container, activity))
					{
						IConnectorClient client = scope.Resolve<IConnectorClient>();
						if (update.MembersAdded.Any())
						{
							Activity reply = activity.CreateReply();
							IEnumerable<ChannelAccount> newMembers = update.MembersAdded?.Where(t => t.Id != activity.Recipient.Id);
							foreach (var newMember in newMembers)
							{
								reply.Text = "The **[GitHub Information Bot](https://docs.microsoft.com/en-us/sandbox/demos/githubbot)** is a sample application that demonstrates how to use " +
								             "[LUIS.ai](https://luis.ai/), [Bot Framework](https://dev.botframework.com/), and [Octokit.NET](https://github.com/octokit/octokit.net) to interact " +
								             "with your GitHub account, repos, issues, etc. from within a chat window. " +
								             "For more information on how it was made, please see its article on [The Sandbox](https://docs.microsoft.com/en-us/sandbox/demos/githubbot). " +
								             "Please note that this is strictly a **demo**, not a supported product, and is subject to [GitHub's rate limiting policy](https://developer.github.com/v3/#rate-limiting). " +
								             "Enjoy!" + Environment.NewLine + Environment.NewLine + "---" + Environment.NewLine + Environment.NewLine;

								reply.Text += $"Welcome {newMember.Name}! I can help you with getting information about your GitHub repos.";

								IBotData data = scope.Resolve<IBotData>();	
								await data.LoadAsync(CancellationToken.None);
								if(data.UserData.ContainsKey(Constants.AuthTokenKey))
									reply.Text += " It looks like you're already logged in, so what can I help you with?";
								else
									reply.Text += " To get started, type **login** to authorize me to talk to GitHub on your behalf, or type **help** to get more information.";

								await client.Conversations.ReplyToActivityAsync(reply);
							}
						}
					}
					break;
			}

			return new HttpResponseMessage(HttpStatusCode.Accepted);
		}
	}
}