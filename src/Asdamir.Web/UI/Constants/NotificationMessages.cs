// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Web.UI.Constants;

/// <summary>
/// Standard notification message keys for localization
/// Use these constants instead of hardcoded strings
/// </summary>
public static class NotificationMessages
{
    // Common CRUD Operations

    /// <summary>
    /// Localization key for the toast shown after an existing record is persisted successfully;
    /// resolves to the culture-specific "saved" confirmation string.
    /// </summary>
    public const string SavedSuccessfully = "Notification.SavedSuccessfully";

    /// <summary>
    /// Localization key for the toast confirming a brand-new record was created;
    /// resolves to the culture-specific "created" confirmation string.
    /// </summary>
    public const string CreatedSuccessfully = "Notification.CreatedSuccessfully";

    /// <summary>
    /// Localization key for the toast confirming an edit to an existing record was applied;
    /// resolves to the culture-specific "updated" confirmation string.
    /// </summary>
    public const string UpdatedSuccessfully = "Notification.UpdatedSuccessfully";

    /// <summary>
    /// Localization key for the toast confirming a record was removed;
    /// resolves to the culture-specific "deleted" confirmation string.
    /// </summary>
    public const string DeletedSuccessfully = "Notification.DeletedSuccessfully";

    /// <summary>
    /// Localization key for the error shown when persisting a record fails;
    /// resolves to the culture-specific "save failed" message.
    /// </summary>
    public const string SaveFailed = "Notification.SaveFailed";

    /// <summary>
    /// Localization key for the error shown when creating a new record fails;
    /// resolves to the culture-specific "create failed" message.
    /// </summary>
    public const string CreateFailed = "Notification.CreateFailed";

    /// <summary>
    /// Localization key for the error shown when updating an existing record fails;
    /// resolves to the culture-specific "update failed" message.
    /// </summary>
    public const string UpdateFailed = "Notification.UpdateFailed";

    /// <summary>
    /// Localization key for the error shown when deleting a record fails;
    /// resolves to the culture-specific "delete failed" message.
    /// </summary>
    public const string DeleteFailed = "Notification.DeleteFailed";

    /// <summary>
    /// Localization key for the error shown when data cannot be fetched/loaded into a view;
    /// resolves to the culture-specific "loading failed" message.
    /// </summary>
    public const string LoadingFailed = "Notification.LoadingFailed";

    /// <summary>
    /// Localization key for the generic fallback error when an operation fails and no more
    /// specific message applies; resolves to the culture-specific "operation failed" string.
    /// </summary>
    public const string OperationFailed = "Notification.OperationFailed";

    // Validation

    /// <summary>
    /// Localization key for the notice shown when submitted input fails validation;
    /// resolves to the culture-specific "validation error" message.
    /// </summary>
    public const string ValidationError = "Notification.ValidationError";

    /// <summary>
    /// Localization key for the message telling the user a mandatory field was left empty;
    /// resolves to the culture-specific "required field" prompt.
    /// </summary>
    public const string RequiredField = "Notification.RequiredField";

    /// <summary>
    /// Localization key for the message telling the user a value does not match the expected format;
    /// resolves to the culture-specific "invalid format" prompt.
    /// </summary>
    public const string InvalidFormat = "Notification.InvalidFormat";

    // Authentication & Authorization

    /// <summary>
    /// Localization key for the confirmation shown after a successful sign-in;
    /// resolves to the culture-specific "login successful" message.
    /// </summary>
    public const string LoginSuccessful = "Notification.LoginSuccessful";

    /// <summary>
    /// Localization key for the message shown when sign-in is rejected (bad credentials, etc.);
    /// resolves to the culture-specific "login failed" message.
    /// </summary>
    public const string LoginFailed = "Notification.LoginFailed";

    /// <summary>
    /// Localization key for the confirmation shown after the user signs out;
    /// resolves to the culture-specific "logout successful" message.
    /// </summary>
    public const string LogoutSuccessful = "Notification.LogoutSuccessful";

    /// <summary>
    /// Localization key for the notice shown when a request is not authenticated (no/invalid identity);
    /// resolves to the culture-specific "unauthorized" message.
    /// </summary>
    public const string Unauthorized = "Notification.Unauthorized";

    /// <summary>
    /// Localization key for the notice shown when an authenticated user lacks permission for an action;
    /// resolves to the culture-specific "access denied" message.
    /// </summary>
    public const string AccessDenied = "Notification.AccessDenied";

    // Network & API

    /// <summary>
    /// Localization key for the error shown when the client cannot reach the API/server (connectivity);
    /// resolves to the culture-specific "network error" message.
    /// </summary>
    public const string NetworkError = "Notification.NetworkError";

    /// <summary>
    /// Localization key for the error shown when a request exceeds its allotted time;
    /// resolves to the culture-specific "timeout" message.
    /// </summary>
    public const string TimeoutError = "Notification.TimeoutError";

    /// <summary>
    /// Localization key for the error shown when the server returns a 5xx/internal failure;
    /// resolves to the culture-specific "server error" message.
    /// </summary>
    public const string ServerError = "Notification.ServerError";

    /// <summary>
    /// Localization key for the notice shown when a requested resource does not exist (404);
    /// resolves to the culture-specific "not found" message.
    /// </summary>
    public const string NotFound = "Notification.NotFound";

    // Confirmation

    /// <summary>
    /// Localization key for the prompt asking the user to confirm a destructive delete before it runs;
    /// resolves to the culture-specific "confirm delete" question.
    /// </summary>
    public const string ConfirmDelete = "Notification.ConfirmDelete";

    /// <summary>
    /// Localization key for the prompt asking the user to confirm saving their changes;
    /// resolves to the culture-specific "confirm save" question.
    /// </summary>
    public const string ConfirmSave = "Notification.ConfirmSave";

    /// <summary>
    /// Localization key for the prompt asking the user to confirm cancelling (and discarding changes);
    /// resolves to the culture-specific "confirm cancel" question.
    /// </summary>
    public const string ConfirmCancel = "Notification.ConfirmCancel";

    /// <summary>
    /// Localization key for the generic confirmation prompt used when no more specific one applies;
    /// resolves to the culture-specific "confirm action" question.
    /// </summary>
    public const string ConfirmAction = "Notification.ConfirmAction";

    // Progress

    /// <summary>
    /// Localization key for the busy indicator text shown while content is being fetched;
    /// resolves to the culture-specific "loading…" label.
    /// </summary>
    public const string Loading = "Notification.Loading";

    /// <summary>
    /// Localization key for the busy indicator text shown while a record is being persisted;
    /// resolves to the culture-specific "saving…" label.
    /// </summary>
    public const string Saving = "Notification.Saving";

    /// <summary>
    /// Localization key for the busy indicator text shown while a longer operation runs;
    /// resolves to the culture-specific "processing…" label.
    /// </summary>
    public const string Processing = "Notification.Processing";

    /// <summary>
    /// Localization key for the generic "please wait" text shown during any pending operation;
    /// resolves to the culture-specific "please wait" label.
    /// </summary>
    public const string PleaseWait = "Notification.PleaseWait";

    // Data Operations

    /// <summary>
    /// Localization key for the empty-state message shown when a query/grid returns no rows;
    /// resolves to the culture-specific "no data found" label.
    /// </summary>
    public const string NoDataFound = "Notification.NoDataFound";

    /// <summary>
    /// Localization key for the confirmation that a data set finished loading successfully;
    /// resolves to the culture-specific "data loaded" message.
    /// </summary>
    public const string DataLoadedSuccessfully = "Notification.DataLoadedSuccessfully";

    /// <summary>
    /// Localization key for the confirmation shown after data is exported to a file;
    /// resolves to the culture-specific "export successful" message.
    /// </summary>
    public const string ExportSuccessful = "Notification.ExportSuccessful";

    /// <summary>
    /// Localization key for the confirmation shown after data is imported from a file;
    /// resolves to the culture-specific "import successful" message.
    /// </summary>
    public const string ImportSuccessful = "Notification.ImportSuccessful";

    // Cache & Performance

    /// <summary>
    /// Localization key for the confirmation shown after a cache is cleared/invalidated;
    /// resolves to the culture-specific "cache cleared" message.
    /// </summary>
    public const string CacheCleared = "Notification.CacheCleared";

    /// <summary>
    /// Localization key for the confirmation shown after a view/data set is refreshed;
    /// resolves to the culture-specific "refresh successful" message.
    /// </summary>
    public const string RefreshSuccessful = "Notification.RefreshSuccessful";

    // Localization Specific

    /// <summary>
    /// Localization key for the confirmation shown after the user switches the active UI language;
    /// resolves to the culture-specific "language changed" message.
    /// </summary>
    public const string LanguageChanged = "Notification.LanguageChanged";

    /// <summary>
    /// Localization key for the confirmation shown after a translation resource entry is saved
    /// (localization management screens); resolves to the culture-specific "translation saved" message.
    /// </summary>
    public const string TranslationSaved = "Notification.TranslationSaved";

    /// <summary>
    /// Localization key for the confirmation shown after a translation resource entry is deleted
    /// (localization management screens); resolves to the culture-specific "translation deleted" message.
    /// </summary>
    public const string TranslationDeleted = "Notification.TranslationDeleted";

    // File Operations

    /// <summary>
    /// Localization key for the confirmation shown after a file upload completes;
    /// resolves to the culture-specific "file uploaded" message.
    /// </summary>
    public const string FileUploadSuccessful = "Notification.FileUploadSuccessful";

    /// <summary>
    /// Localization key for the error shown when a file upload fails;
    /// resolves to the culture-specific "file upload failed" message.
    /// </summary>
    public const string FileUploadFailed = "Notification.FileUploadFailed";

    /// <summary>
    /// Localization key for the confirmation shown after a file download completes;
    /// resolves to the culture-specific "file downloaded" message.
    /// </summary>
    public const string FileDownloadSuccessful = "Notification.FileDownloadSuccessful";

    /// <summary>
    /// Localization key for the error telling the user the selected file exceeds the size limit;
    /// resolves to the culture-specific "file too large" message.
    /// </summary>
    public const string FileTooLarge = "Notification.FileTooLarge";

    /// <summary>
    /// Localization key for the error telling the user the selected file type is not allowed;
    /// resolves to the culture-specific "invalid file type" message.
    /// </summary>
    public const string InvalidFileType = "Notification.InvalidFileType";
}

/// <summary>
/// Standard notification titles for dialogs
/// </summary>
public static class NotificationTitles
{
    /// <summary>
    /// Localization key for the header of a success dialog/toast (positive outcomes);
    /// resolves to the culture-specific "Success" title.
    /// </summary>
    public const string Success = "Notification.Title.Success";

    /// <summary>
    /// Localization key for the header of an error dialog/toast (failures);
    /// resolves to the culture-specific "Error" title.
    /// </summary>
    public const string Error = "Notification.Title.Error";

    /// <summary>
    /// Localization key for the header of a warning dialog/toast (caution, non-fatal);
    /// resolves to the culture-specific "Warning" title.
    /// </summary>
    public const string Warning = "Notification.Title.Warning";

    /// <summary>
    /// Localization key for the header of an informational dialog/toast (neutral notices);
    /// resolves to the culture-specific "Information" title.
    /// </summary>
    public const string Information = "Notification.Title.Information";

    /// <summary>
    /// Localization key for the header of a confirmation dialog that asks the user to proceed;
    /// resolves to the culture-specific "Confirmation" title.
    /// </summary>
    public const string Confirmation = "Notification.Title.Confirmation";

    /// <summary>
    /// Localization key for the header of a delete-confirmation dialog;
    /// resolves to the culture-specific "Delete" title.
    /// </summary>
    public const string Delete = "Notification.Title.Delete";
}
