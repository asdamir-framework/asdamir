// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Core.Dtos;

/// <summary>Read/create/update model for a company (firma) — the top-level tenant that groups users and service points in AppManagement.</summary>
public record CompanyDto
{
    /// <summary>Stable unique identifier of the company.</summary>
    public Guid Id { get; init; }
    /// <summary>Full company name including its branch designation (şubeli firma).</summary>
    public string SubeliFirma { get; init; } = string.Empty;
    /// <summary>Short display name of the company used across the UI.</summary>
    public string KisaAdi { get; init; } = string.Empty;
    /// <summary>Official legal/trade title (ünvan) of the company.</summary>
    public string Unvani { get; init; } = string.Empty;
    /// <summary>Accounting/ERP reference code for the company (muhasebe kodu).</summary>
    public string? MuhasebeKodu { get; init; }
    /// <summary>Name of the tax office (vergi dairesi) the company is registered with.</summary>
    public string? VergiDairesi { get; init; }
    /// <summary>Government tax/VAT registration number of the company.</summary>
    public string? VergiNo { get; init; }
    /// <summary>Whether the company is active; inactive companies are hidden from selection.</summary>
    public bool Aktif { get; init; }
    /// <summary>Free-text street address of the company.</summary>
    public string? Adres { get; init; }
    /// <summary>Province (il) reference id for the address.</summary>
    public int? IlId { get; init; }
    /// <summary>District (ilçe) reference id for the address.</summary>
    public int? IlceId { get; init; }
    /// <summary>Branch (şube) reference id the company is grouped under.</summary>
    public int? SubeIdd { get; init; }
    /// <summary>Denormalized branch (şube) name for display.</summary>
    public string? SubeAdi { get; init; }
    /// <summary>Region (bölge) reference id the company belongs to.</summary>
    public int? BolgeIdd { get; init; }
    /// <summary>Denormalized region (bölge) name for display.</summary>
    public string? BolgeAdi { get; init; }
    /// <summary>Start date of the company's service contract (sözleşme başlangıcı).</summary>
    public DateTime? SozlesmeBaslangici { get; init; }
    /// <summary>Soft-delete flag; deleted companies are retained but excluded from normal queries.</summary>
    public bool Deleted { get; init; }
    /// <summary>Denormalized operation-region (operasyon bölgesi) name for display.</summary>
    public string? OperasyonBolgeAdi { get; init; }
    /// <summary>Operation-region (operasyon bölgesi) reference id assigned to the company.</summary>
    public int? OperasyonBolgeId { get; init; }
}

/// <summary>Lightweight company projection for pickers/dropdowns, carrying only id, short name and legal title.</summary>
public record CompanySimpleDto
{
    /// <summary>Stable unique identifier of the company.</summary>
    public Guid Id { get; init; }
    /// <summary>Short display name of the company used across the UI.</summary>
    public string KisaAdi { get; init; } = string.Empty;
    /// <summary>Official legal/trade title (ünvan) of the company.</summary>
    public string Unvani { get; init; } = string.Empty;
}
