// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Microsoft.AspNetCore.Authorization;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Asdamir.Web.Security.Components;

/// <summary>
/// Route authorization handler that automatically redirects unauthorized users
/// </summary>
public class RouteAuthorizationHandler : ComponentBase
{
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private ILogger<RouteAuthorizationHandler> Logger { get; set; } = default!;

    [Parameter] public RenderFragment<AuthenticationState>? Authorized { get; set; }
    [Parameter] public RenderFragment? NotAuthorized { get; set; }
    [Parameter] public RenderFragment? Authorizing { get; set; }
    [Parameter] public IAuthorizationRequirement[]? Requirements { get; set; }
    [Parameter] public string? Roles { get; set; }
    [Parameter] public string? Policy { get; set; }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            
            if (!authState.User.Identity?.IsAuthenticated ?? true)
            {
                Logger.LogWarning("User is not authenticated, redirecting to login");
                NavigationManager.NavigateTo("/login", forceLoad: true);
                return;
            }

            // Check specific authorization requirements if provided
            if (!string.IsNullOrEmpty(Roles))
            {
                var requiredRoles = Roles.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(r => r.Trim());
                
                if (!requiredRoles.Any(role => authState.User.IsInRole(role)))
                {
                    Logger.LogWarning("User {User} does not have required roles: {Roles}", 
                        authState.User.Identity?.Name ?? "Unknown", Roles);
                    await ShowUnauthorizedDialog();
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error checking authorization");
            NavigationManager.NavigateTo("/login", forceLoad: true);
        }
    }

    private async Task ShowUnauthorizedDialog()
    {
        var dialog = await DialogService.ShowInfoAsync("Yetkisiz Erişim", 
            "Bu sayfaya erişim yetkiniz bulunmamaktadır.");
        
        var result = await dialog.Result;
        NavigationManager.NavigateTo("/", forceLoad: true);
    }
}
