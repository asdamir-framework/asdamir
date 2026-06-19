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
    public event EventHandler<NotificationRequest>? NotificationRequested;
    public event EventHandler<ConfirmationRequest>? ConfirmationRequested;
    public event EventHandler<LoadingRequest>? LoadingChanged;
    public event EventHandler<ActionNotificationRequest>? ActionNotificationRequested;

    private readonly IStringLocalizer? _localizer;
    
    public NotificationService(IStringLocalizer? localizer = null)
    {
        _localizer = localizer;
    }
    
    // Parameterless constructor for backward compatibility
    public NotificationService() : this((IStringLocalizer?)null)
    {
    }

    // Simple methods (backward compatible - no localization)
    public void Success(string message, int timeoutMs = 3500)
    {
        Publish(new NotificationRequest(NotificationSeverity.Success, message, timeoutMs));
    }

    public void Info(string message, int timeoutMs = 3500)
    {
        Publish(new NotificationRequest(NotificationSeverity.Info, message, timeoutMs));
    }

    public void Warning(string message, int timeoutMs = 3500)
    {
        Publish(new NotificationRequest(NotificationSeverity.Warning, message, timeoutMs));
    }

    public void Error(string message, int timeoutMs = 5000)
    {
        Publish(new NotificationRequest(NotificationSeverity.Error, message, timeoutMs));
    }

    // Localized methods
    public void ShowSuccess(string messageKey, params object[] args)
    {
        var message = GetLocalizedMessage(messageKey, args);
        Publish(new NotificationRequest(NotificationSeverity.Success, message, 3500));
    }

    public void ShowInfo(string messageKey, params object[] args)
    {
        var message = GetLocalizedMessage(messageKey, args);
        Publish(new NotificationRequest(NotificationSeverity.Info, message, 3500));
    }

    public void ShowWarning(string messageKey, params object[] args)
    {
        var message = GetLocalizedMessage(messageKey, args);
        Publish(new NotificationRequest(NotificationSeverity.Warning, message, 4000));
    }

    public void ShowError(string messageKey, params object[] args)
    {
        var message = GetLocalizedMessage(messageKey, args);
        Publish(new NotificationRequest(NotificationSeverity.Error, message, 5000));
    }

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
    public Task<bool> ConfirmAsync(string messageKey, string? titleKey = null, params object[] args)
        => RaiseConfirmation(messageKey, titleKey, isDanger: false, args);

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
    public void ShowLoading(string messageKey, params object[] args)
        => LoadingChanged?.Invoke(this, new LoadingRequest(true, GetLocalizedMessage(messageKey, args)));

    public void HideLoading()
        => LoadingChanged?.Invoke(this, new LoadingRequest(false, null));

    // Persistent notification with action buttons. Falls back to a plain persistent toast when no
    // action-aware host is subscribed, so the message is still shown (just without the buttons).
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
