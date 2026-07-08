// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Microsoft.Extensions.Localization;

namespace Asdamir.Web.UI.Services;

/// <summary>
/// Enterprise notification service implementation with event-based notification bar
/// </summary>
public sealed class NotificationService : INotificationService
{
    /// <inheritdoc/>
    public event EventHandler<NotificationRequest>? NotificationRequested;
    /// <inheritdoc/>
    public event EventHandler<ConfirmationRequest>? ConfirmationRequested;
    /// <inheritdoc/>
    public event EventHandler<LoadingRequest>? LoadingChanged;
    /// <inheritdoc/>
    public event EventHandler<ActionNotificationRequest>? ActionNotificationRequested;

    private readonly IStringLocalizer? _localizer;

    /// <summary>
    /// Creates the service with an optional <see cref="IStringLocalizer"/>. When supplied, the
    /// <c>Show*</c>/<c>Confirm*</c> overloads treat their first argument as a resource key and resolve it
    /// (falling back to the raw key when the resource is missing); when <c>null</c>, keys are used verbatim.
    /// </summary>
    /// <param name="localizer">The localizer used to resolve message keys, or <c>null</c> for no localization.</param>
    public NotificationService(IStringLocalizer? localizer = null)
    {
        _localizer = localizer;
    }

    /// <summary>
    /// Creates the service without a localizer (backward-compatible overload); message keys are shown verbatim.
    /// </summary>
    public NotificationService() : this((IStringLocalizer?)null)
    {
    }

    /// <inheritdoc/>
    public void Success(string message, int timeoutMs = 3500)
    {
        Publish(new NotificationRequest(NotificationSeverity.Success, message, timeoutMs));
    }

    /// <inheritdoc/>
    public void Info(string message, int timeoutMs = 3500)
    {
        Publish(new NotificationRequest(NotificationSeverity.Info, message, timeoutMs));
    }

    /// <inheritdoc/>
    public void Warning(string message, int timeoutMs = 3500)
    {
        Publish(new NotificationRequest(NotificationSeverity.Warning, message, timeoutMs));
    }

    /// <inheritdoc/>
    public void Error(string message, int timeoutMs = 5000)
    {
        Publish(new NotificationRequest(NotificationSeverity.Error, message, timeoutMs));
    }

    /// <inheritdoc/>
    public void ShowSuccess(string messageKey, params object[] args)
    {
        var message = GetLocalizedMessage(messageKey, args);
        Publish(new NotificationRequest(NotificationSeverity.Success, message, 3500));
    }

    /// <inheritdoc/>
    public void ShowInfo(string messageKey, params object[] args)
    {
        var message = GetLocalizedMessage(messageKey, args);
        Publish(new NotificationRequest(NotificationSeverity.Info, message, 3500));
    }

    /// <inheritdoc/>
    public void ShowWarning(string messageKey, params object[] args)
    {
        var message = GetLocalizedMessage(messageKey, args);
        Publish(new NotificationRequest(NotificationSeverity.Warning, message, 4000));
    }

    /// <inheritdoc/>
    public void ShowError(string messageKey, params object[] args)
    {
        var message = GetLocalizedMessage(messageKey, args);
        Publish(new NotificationRequest(NotificationSeverity.Error, message, 5000));
    }

    /// <inheritdoc/>
    public void Show(NotificationOptions options)
    {
        var message = GetLocalizedMessage(options.MessageKey, options.MessageArgs);
        Publish(new NotificationRequest(
            options.Severity, 
            message, 
            options.TimeoutMs,
            options.Icon,
            options.CorrelationId));
    }

    // Confirmation dialogs — raise an event carrying a TaskCompletionSource; the host
    // (AsdamirNotificationHost) shows the dialog and completes it with the user's choice.
    /// <inheritdoc/>
    public Task<bool> ConfirmAsync(string messageKey, string? titleKey = null, params object[] args)
        => RaiseConfirmation(messageKey, titleKey, isDanger: false, args);

    /// <inheritdoc/>
    public Task<bool> ConfirmDangerAsync(string messageKey, string? titleKey = null, params object[] args)
        => RaiseConfirmation(messageKey, titleKey, isDanger: true, args);

    private Task<bool> RaiseConfirmation(string messageKey, string? titleKey, bool isDanger, object[] args)
    {
        var handler = ConfirmationRequested;
        if (handler is null)
        {
            // No confirmation UI is wired (no <AsdamirNotificationHost/> on the page). DENY rather
            // than silently allow — a destructive action must never proceed without a real prompt.
            return Task.FromResult(false);
        }

        // RunContinuationsAsynchronously: don't run the awaiting code inline on the UI event dispatch.
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var message = GetLocalizedMessage(messageKey, args);
        var title = string.IsNullOrEmpty(titleKey) ? null : GetLocalizedMessage(titleKey);
        handler.Invoke(this, new ConfirmationRequest(message, title, isDanger, completion));
        return completion.Task;
    }

    // Loading overlay — host shows/hides it.
    /// <inheritdoc/>
    public void ShowLoading(string messageKey, params object[] args)
        => LoadingChanged?.Invoke(this, new LoadingRequest(true, GetLocalizedMessage(messageKey, args)));

    /// <inheritdoc/>
    public void HideLoading()
        => LoadingChanged?.Invoke(this, new LoadingRequest(false, null));

    // Persistent notification with action buttons. Falls back to a plain persistent toast when no
    // action-aware host is subscribed, so the message is still shown (just without the buttons).
    /// <inheritdoc/>
    public void ShowWithActions(string messageKey, List<NotificationAction> actions, params object[] args)
    {
        var message = GetLocalizedMessage(messageKey, args);
        var handler = ActionNotificationRequested;
        if (handler is not null)
            handler.Invoke(this, new ActionNotificationRequest(NotificationSeverity.Info, message, actions));
        else
            Publish(new NotificationRequest(NotificationSeverity.Info, message, 0)); // persistent fallback
    }

    private string GetLocalizedMessage(string messageKey, params object[] args)
    {
        if (_localizer == null)
        {
            // No localization - return key or format with args
            return args != null && args.Length > 0 ? string.Format(messageKey, args) : messageKey;
        }

        // Get localized string
        var localizedString = args != null && args.Length > 0 
            ? _localizer[messageKey, args]
            : _localizer[messageKey];
        
        // Return localized value or fallback to key if not found
        return localizedString.ResourceNotFound ? messageKey : localizedString.Value;
    }

    private void Publish(NotificationRequest request)
    {
        NotificationRequested?.Invoke(this, request);
    }
}
