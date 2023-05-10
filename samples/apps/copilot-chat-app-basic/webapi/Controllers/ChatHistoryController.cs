﻿// Copyright (c) Microsoft. All rights reserved.

using Microsoft.AspNetCore.Mvc;
using SemanticKernel.Service.Model;
using SemanticKernel.Service.Skills;
using SemanticKernel.Service.Storage;

namespace SemanticKernel.Service.Controllers;

/// <summary>
/// Controller for chat history.
/// This controller is responsible for creating new chat sessions, retrieving chat sessions,
/// retrieving chat messages, and editing chat sessions.
/// </summary>
[ApiController]
public class ChatHistoryController : ControllerBase
{
    private readonly ILogger<ChatHistoryController> _logger;
    private readonly ChatSessionRepository _chatSessionRepository;
    private readonly ChatMessageRepository _chatMessageRepository;
    private readonly PromptSettings _promptSettings;
    private const string GuestChatId = "461a6d36-967e-40b1-93e1-3830fcd95e6d";

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatHistoryController"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="chatSessionRepository">The chat session repository.</param>
    /// <param name="chatMessageRepository">The chat message repository.</param>
    /// <param name="promptSettings">The prompt settings.</param>
    public ChatHistoryController(
        ILogger<ChatHistoryController> logger,
        ChatSessionRepository chatSessionRepository,
        ChatMessageRepository chatMessageRepository,
        PromptSettings promptSettings)
    {
        this._logger = logger;
        this._chatSessionRepository = chatSessionRepository;
        this._chatMessageRepository = chatMessageRepository;
        this._promptSettings = promptSettings;
    }

    /// <summary>
    /// Create a new chat session and populate the session with the initial bot message.
    /// </summary>
    /// <param name="chatParameters">Object that contains the parameters to create a new chat.</param>
    /// <returns>The HTTP action result.</returns>
    [HttpPost]
    [Route("chatSession/create")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateChatSessionAsync(
        [FromBody] ChatSession chatParameters)
    {
        var userId = chatParameters.UserId;
        var title = chatParameters.Title;

        try
        {
            var chat = await this._chatSessionRepository.FindByIdAsync(GuestChatId);
            return this.Ok(chat);
        }
        catch (Exception ex)
        {
            var newChat = new ChatSession(userId, title, GuestChatId);
            await this._chatSessionRepository.CreateAsync(newChat);

            var initialBotMessage = this._promptSettings.InitialBotMessage;
            await this.SaveResponseAsync(initialBotMessage, GuestChatId);

            this._logger.LogDebug("Created chat session with id {0} for user {1}", GuestChatId, userId);
            return this.CreatedAtAction(nameof(this.GetChatSessionByIdAsync), new { chatId = GuestChatId }, newChat);
        }
    }

    /// <summary>
    /// Get a chat session by id.
    /// </summary>
    /// <param name="chatId">The chat id.</param>
    [HttpGet]
    [ActionName("GetChatSessionByIdAsync")]
    [Route("chatSession/getChat/{chatId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChatSessionByIdAsync(Guid chatId)
    {
        var chat = await this._chatSessionRepository.FindByIdAsync(chatId.ToString());
        if (chat == null)
        {
            return this.NotFound($"Chat of id {chatId} not found.");
        }

        return this.Ok(chat);
    }

    /// <summary>
    /// Get all chat messages for a chat session.
    /// The list will be ordered with the first entry being the most recent message.
    /// </summary>
    /// <param name="chatId">The chat id.</param>
    /// <param name="startIdx">The start index at which the first message will be returned.</param>
    /// <param name="count">The number of messages to return. -1 will return all messages starting from startIdx.</param>
    /// [Authorize]
    [HttpGet]
    [Route("chatSession/getChatMessages/{chatId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChatMessagesAsync(
        Guid chatId,
        [FromQuery] int startIdx = 0,
        [FromQuery] int count = -1)
    {
        // TODO: the code mixes strings and Guid without being explicit about the serialization format
        var chatMessages = await this._chatMessageRepository.FindByChatIdAsync(chatId.ToString());
        if (chatMessages == null)
        {
            return this.NotFound($"No messages found for chat id '{chatId}'.");
        }

        chatMessages = chatMessages.OrderByDescending(m => m.Timestamp).Skip(startIdx);
        if (count >= 0) { chatMessages = chatMessages.Take(count); }

        return this.Ok(chatMessages);
    }

    /// <summary>
    /// Edit a chat session.
    /// </summary>
    /// <param name="chatParameters">Object that contains the parameters to edit the chat.</param>
    [HttpPost]
    [Route("chatSession/edit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EditChatSessionAsync([FromBody] ChatSession chatParameters)
    {
        string chatId = chatParameters.Id;

        ChatSession? chat = await this._chatSessionRepository.FindByIdAsync(chatId);
        if (chat == null)
        {
            return this.NotFound($"Chat of id {chatId} not found.");
        }

        chat.Title = chatParameters.Title;
        await this._chatSessionRepository.UpdateAsync(chat);

        return this.Ok(chat);
    }

    # region Private

    /// <summary>
    /// Save a bot response to the chat session.
    /// </summary>
    /// <param name="response">The bot response.</param>
    /// <param name="chatId">The chat id.</param>
    private async Task SaveResponseAsync(string response, string chatId)
    {
        // Make sure the chat session exists
        await this._chatSessionRepository.FindByIdAsync(chatId);

        var chatMessage = ChatMessage.CreateBotResponseMessage(chatId, response);
        await this._chatMessageRepository.CreateAsync(chatMessage);
    }

    # endregion
}