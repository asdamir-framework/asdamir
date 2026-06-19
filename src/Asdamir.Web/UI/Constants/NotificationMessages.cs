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
    public const string SavedSuccessfully = "Notification.SavedSuccessfully";
    public const string CreatedSuccessfully = "Notification.CreatedSuccessfully";
    public const string UpdatedSuccessfully = "Notification.UpdatedSuccessfully";
    public const string DeletedSuccessfully = "Notification.DeletedSuccessfully";
    
    public const string SaveFailed = "Notification.SaveFailed";
    public const string CreateFailed = "Notification.CreateFailed";
    public const string UpdateFailed = "Notification.UpdateFailed";
    public const string DeleteFailed = "Notification.DeleteFailed";
    
    public const string LoadingFailed = "Notification.LoadingFailed";
    public const string OperationFailed = "Notification.OperationFailed";
    
    // Validation
    public const string ValidationError = "Notification.ValidationError";
    public const string RequiredField = "Notification.RequiredField";
    public const string InvalidFormat = "Notification.InvalidFormat";
    
    // Authentication & Authorization
    public const string LoginSuccessful = "Notification.LoginSuccessful";
    public const string LoginFailed = "Notification.LoginFailed";
    public const string LogoutSuccessful = "Notification.LogoutSuccessful";
    public const string Unauthorized = "Notification.Unauthorized";
    public const string AccessDenied = "Notification.AccessDenied";
    
    // Network & API
    public const string NetworkError = "Notification.NetworkError";
    public const string TimeoutError = "Notification.TimeoutError";
    public const string ServerError = "Notification.ServerError";
    public const string NotFound = "Notification.NotFound";
    
    // Confirmation
    public const string ConfirmDelete = "Notification.ConfirmDelete";
    public const string ConfirmSave = "Notification.ConfirmSave";
    public const string ConfirmCancel = "Notification.ConfirmCancel";
    public const string ConfirmAction = "Notification.ConfirmAction";
    
    // Progress
    public const string Loading = "Notification.Loading";
    public const string Saving = "Notification.Saving";
    public const string Processing = "Notification.Processing";
    public const string PleaseWait = "Notification.PleaseWait";
    
    // Data Operations
    public const string NoDataFound = "Notification.NoDataFound";
    public const string DataLoadedSuccessfully = "Notification.DataLoadedSuccessfully";
    public const string ExportSuccessful = "Notification.ExportSuccessful";
    public const string ImportSuccessful = "Notification.ImportSuccessful";
    
    // Cache & Performance
    public const string CacheCleared = "Notification.CacheCleared";
    public const string RefreshSuccessful = "Notification.RefreshSuccessful";
    
    // Localization Specific
    public const string LanguageChanged = "Notification.LanguageChanged";
    public const string TranslationSaved = "Notification.TranslationSaved";
    public const string TranslationDeleted = "Notification.TranslationDeleted";
    
    // File Operations
    public const string FileUploadSuccessful = "Notification.FileUploadSuccessful";
    public const string FileUploadFailed = "Notification.FileUploadFailed";
    public const string FileDownloadSuccessful = "Notification.FileDownloadSuccessful";
    public const string FileTooLarge = "Notification.FileTooLarge";
    public const string InvalidFileType = "Notification.InvalidFileType";
}

/// <summary>
/// Standard notification titles for dialogs
/// </summary>
public static class NotificationTitles
{
    public const string Success = "Notification.Title.Success";
    public const string Error = "Notification.Title.Error";
    public const string Warning = "Notification.Title.Warning";
    public const string Information = "Notification.Title.Information";
    public const string Confirmation = "Notification.Title.Confirmation";
    public const string Delete = "Notification.Title.Delete";
}
