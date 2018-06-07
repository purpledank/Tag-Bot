﻿using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TagBot.Entities;

namespace TagBot.Services
{
    public class MessageService
    {
        private readonly Timer _timer;
        private readonly DiscordSocketClient _client;
        private List<MessageModel> _messages = new List<MessageModel>();
        
        public MessageService(DiscordSocketClient client)
        {
            _client = client;
            _timer = new Timer(async _ =>
                {
                    if (!_messages.Any()) return;
                    var currentMessage = _messages.FirstOrDefault();
                    var channel = _client.GetChannel(currentMessage.channelId) as SocketTextChannel;
                    if (channel is null) return;
                    var msg = await channel.GetMessageAsync(currentMessage.messageId);
                    await msg.DeleteAsync();
                    _messages.Remove(currentMessage);
                    SetTime();
                }, 
                null, 
                Timeout.Infinite, 
                Timeout.Infinite);
        }

        public async Task SendMessage(SocketCommandContext context, string message, TimeSpan? timeout)
        {
            var msg = await context.Channel.SendMessageAsync(message);
            var item = new MessageModel(context.User.Id, context.Channel.Id, msg.Id, (timeout.HasValue ? DateTime.UtcNow.Add(timeout.Value) : DateTime.MaxValue), msg.CreatedAt);
            _messages.Add(item);
            _messages = _messages.OrderBy(x => x.timeout).ToList();
            SetTime();
        }

        private void SetTime()
        {
            CleanseMessages();
            if (!_messages.Any()) return;
            var currentMessage = _messages.FirstOrDefault();
            if (currentMessage.timeout < DateTime.UtcNow)
            {
                _messages.Remove(currentMessage);
                return;
            }

            if (currentMessage.timeout == DateTime.MaxValue) return;
            _timer.Change(currentMessage.timeout - DateTime.UtcNow, TimeSpan.Zero);
        }

        public async Task ClearMessages(SocketCommandContext context)
        {
            CleanseMessages();
            var ids = _messages.Where(x => x.userId == context.User.Id && x.channelId == context.Channel.Id).Select(y => y.messageId).ToList();
            if (!ids.Any()) return;
            var messages = new List<IMessage>();
            foreach (var id in ids)
            {
                messages.Add(context.Channel.GetCachedMessage(id));
                _messages.Remove(_messages.FirstOrDefault(x => x.messageId == id));
            }

            if (!messages.Any()) return;
            var channel = context.Channel as SocketTextChannel;
            foreach (var msg in messages)
            {
                await channel.DeleteMessageAsync(msg);
            }
            SetTime();
        }

        private void CleanseMessages()
        {
            foreach (var msg in _messages)
                if (DateTimeOffset.UtcNow - msg.createdAt > TimeSpan.FromMinutes(5))
                    _messages.Remove(msg);
        }
    }
}
