// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Web.UI.Services;

/// <summary>
/// Enterprise-grade notification service with localization, confirmation, and structured logging
/// </summary>
public interface INotificationService
{
    // Event for notification bar component
    event EventHandler<NotificationRequest>? NotificationRequested;

    // Events the host component (AsdamirNotificationHost) subscribes to: confirmation dialogs,
    // the loading overlay, and action toasts. When no host is subscribed, ConfirmAsync DENIES
    // (returns false) — a destructive action is never allowed to proceed un-confirmed.
    event EventHandler<ConfirmationRequest>? ConfirmationRequested;
    event EventHandler<LoadingRequest>? LoadingChanged;
    event EventHandler<ActionNotificationRequest>? ActionNotificationRequested;

    // Simple Toast Notifications (backward compatible)
    void Success(string message, int timeoutMs = 3500);
    void Info(string message, int timeoutMs = 3500);
    void Warning(string message, int timeoutMs = 3500);
    void Error(string message, int timeoutMs = 5000);
    
    // Localized Toast Notifications  
    void ShowSuccess(string messageKey, params object[] args);
    void ShowInfo(string messageKey, params object[] args);
    void ShowWarning(string messageKey, params object[] args);
    void ShowError(string messageKey, params object[] args);
    
    // With custom options
    void Show(NotificationOptions options);
    
    // Confirmation Dialogs
    Task<bool> ConfirmAsync(string messageKey, string? titleKey = null, params object[] args);
    Task<bool> ConfirmDangerAsync(string messageKey, string? titleKey = null, params object[] args);
    
    // Progress/Loading
    void ShowLoading(string messageKey, params object[] args);
    void HideLoading();
    
    // Custom Actions
    void ShowWithActions(string messageKey, List<NotificationAction> actions, params object[] args);
}

public class NotificationOptions
{
    public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;
    public string MessageKey { get; set; } = string.Empty;
    public object[] MessageArgs { get; set; } = Array.Empty<object>();
    public string? TitleKey { get; set; }
    public int TimeoutMs { get; set; } = 3500;
    public bool IsPersistent { get; set; }
    public bool AllowDismiss { get; set; } = true;
    public List<NotificationAction>? Actions { get; set; }
    public NotificationPosition Position { get; set; } = NotificationPosition.TopRight;
    public bool EnableSound { get; set; } = true;
    public string? Icon { get; set; }
    public string? CorrelationId { get; set; } // For logging
}

public class NotificationAction
{
    public string LabelKey { get; set; } = string.Empty;
    public Action? OnClick { get; set; }
    public bool CloseOnClick { get; set; } = true;
    public NotificationActionStyle Style { get; set; } = NotificationActionStyle.Default;
}

public enum NotificationSeverity
{
    Info,
    Success,
    Warning,
    Error,
    Critical
}

public enum NotificationPosition
{
    TopLeft,
    TopCenter,
    TopRight,
    BottomLeft,
    BottomCenter,
    BottomRight
}

public enum NotificationActionStyle
{
    Default,
    Primary,
    Danger
}

/// <summary>
/// Notification request for event-based communication between service and Asdamir.Web.UI component
/// </summary>
public sealed record NotificationRequest(
    NotificationSeverity Severity,
    string Message,
    int TimeoutMs,
    string? Icon = null,
    string? CorrelationId = null);

/// <summary>
/// A confirmation prompt awaiting a yes/no answer. The host completes <see cref="Completion"/> with the
/// user's choice; <see cref="INotificationService.ConfirmAsync"/> awaits that task.
/// </summary>
public sealed record ConfirmationRequest(
    string Message,
    string? Title,
    bool IsDanger,
    TaskCompletionSource<bool> Completion);

/// <summary>Show/hide the loading overlay (with an optional message when showing).</summary>
public sealed record LoadingRequest(bool IsVisible, string? Message);

/// <summary>A persistent notification carrying clickable action buttons.</summary>
public sealed record ActionNotificationRequest(
    NotificationSeverity Severity,
    string Message,
    IReadOnlyList<NotificationAction> Actions);
