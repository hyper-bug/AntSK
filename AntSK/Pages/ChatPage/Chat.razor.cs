﻿using AntDesign;
using AntSK.Domain.Model;
using AntSK.Domain.Repositories;
using AntSK.Domain.Utils;
using Azure.Core;
using MarkdownSharp;
using Microsoft.AspNetCore.Components;
using Microsoft.KernelMemory;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using SqlSugar;
using System;

namespace AntSK.Pages.ChatPage
{
    public partial class Chat
    {
        [Parameter]
        public string AppId { get; set; }
        [Inject] 
        protected MessageService? Message { get; set; }
        [Inject]
        protected IApps_Repositories _apps_Repositories { get; set; }
        [Inject]
        protected IKmss_Repositories _kmss_Repositories { get; set; }
        [Inject]
        protected IKmsDetails_Repositories _kmsDetails_Repositories { get; set; }
        [Inject]
        protected MemoryServerless _memory { get; set; }

        protected bool _loading = false;
        protected List<MessageInfo> MessageList = [];
        protected string? _messageInput;
        protected string _json = "";

        List<RelevantSource> RelevantSources = new List<RelevantSource>();

        protected List<Apps> _list = new List<Apps>();
        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            _list= _apps_Repositories.GetList();
        }
        protected async Task OnSendAsync()
        {
            if (string.IsNullOrWhiteSpace(_messageInput))
            {
                _ = Message.Info("请输入消息", 2);
                return;
            }

            if (string.IsNullOrWhiteSpace(AppId))
            {
                _ = Message.Info("请选择应用进行测试", 2);
                return;
            }

            await SendAsync(_messageInput);
            _messageInput = "";

        }
        protected async Task OnCopyAsync(MessageInfo item)
        {
            await Task.Run(() =>
            {
                _messageInput = item.Questions;
            });
        }

        protected async Task OnClearAsync(string id)
        {
            await Task.Run(() =>
            {
                MessageList = MessageList.Where(w => w.ID != id).ToList();
            });
        }

        protected async Task<bool> SendAsync(string questions)
        {
            Apps app=_apps_Repositories.GetFirst(p => p.Id == AppId);
            switch (app.Type)
            {
                case "chat":
                    break;
                case "kms":
                    var filters = new List<MemoryFilter>();

                    var kmsidList = app.KmsIdList.Split(",");
                    foreach (var kmsid in kmsidList)
                    {
                        filters.Add(new MemoryFilter().ByTag("kmsid", kmsid));
                    }

                    var result = await _memory.AskAsync(questions, index: "kms", filters: filters);
                    if (result!=null)
                    {
                        if (!string.IsNullOrEmpty(result.Result))
                        {
                            string answers = result.Result;
                            var markdown = new Markdown();
                            string htmlAnswers = markdown.Transform(answers);
                            var info = new MessageInfo()
                            {
                                ID = Guid.NewGuid().ToString(),
                                Questions = questions,
                                Answers = answers,
                                HtmlAnswers = htmlAnswers,
                                CreateTime = DateTime.Now,
                            };
                            MessageList.Add(info);
                        }
     
                        foreach (var x in result.RelevantSources)
                        {
                            foreach (var xsd in x.Partitions)
                            {
                                string sourceName = x.SourceName;
                                var fileDetail = _kmsDetails_Repositories.GetFirst(p => p.FileGuidName == x.SourceName);
                                if (fileDetail.IsNotNull())
                                {
                                    sourceName = fileDetail.FileName;
                                }
                                RelevantSources.Add(new RelevantSource() { SourceName = sourceName, Text = xsd.Text, Relevance = xsd.Relevance });
                            }
                        }
                    }
                    break;
            }

            return await Task.FromResult(true);
        }
    }

    public class RelevantSource
    {
        public string SourceName { get; set; }

        public string Text { get; set; }
        public float Relevance { get; set; }
    }
}
