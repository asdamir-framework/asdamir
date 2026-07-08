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

/// <summary>Read/create/update model for a service point (hizmet noktası) — a branch/location that belongs to a company (firma) in AppManagement.</summary>
public record ServicePointDto
{
    /// <summary>Stable unique identifier of the service point.</summary>
    public Guid Id { get; init; }
    /// <summary>Identifier of the company (firma) this service point belongs to.</summary>
    public Guid FirmaId { get; init; }
    /// <summary>Display name of the service point.</summary>
    public string HizmetNoktasiAdi { get; init; } = string.Empty;
    /// <summary>Whether the service point is active; inactive ones are hidden from selection.</summary>
    public bool Aktif { get; init; }
    /// <summary>Free-text street address of the service point.</summary>
    public string? Adres { get; init; }
    /// <summary>Province (il) reference id for the address.</summary>
    public int? IlId { get; init; }
    /// <summary>District (ilçe) reference id for the address.</summary>
    public int? IlceId { get; init; }
    /// <summary>Branch (şube) reference id this service point is grouped under.</summary>
    public int? SubeIdd { get; init; }
    /// <summary>Denormalized branch (şube) name for display.</summary>
    public string? SubeAdi { get; init; }
    /// <summary>Region (bölge) reference id this service point belongs to.</summary>
    public int? BolgeIdd { get; init; }
    /// <summary>Denormalized region (bölge) name for display.</summary>
    public string? BolgeAdi { get; init; }
    /// <summary>Denormalized zone name for display.</summary>
    public string? ZoneAdi { get; init; }
    /// <summary>Name of the authorized contact person at this service point.</summary>
    public string? YetkiliAdi { get; init; }
    /// <summary>Primary contact phone number.</summary>
    public string? Telefon { get; init; }
    /// <summary>Business registration date of the service point.</summary>
    public DateTime? KayitTarihi { get; init; }
    /// <summary>UTC timestamp when the record was created (audit).</summary>
    public DateTime? CreatedAt { get; init; }
    /// <summary>Identity of the user who created the record (audit).</summary>
    public string? CreatedBy { get; init; }
    /// <summary>UTC timestamp of the last update (audit).</summary>
    public DateTime? UpdatedAt { get; init; }
    /// <summary>Identity of the user who last updated the record (audit).</summary>
    public string? UpdatedBy { get; init; }
    /// <summary>Ordering sequence number of the service point within its company (FHN sıra no).</summary>
    public int? FhnSiraNo { get; init; }
    /// <summary>Country (ülke) reference id for the address.</summary>
    public int? UlkeId { get; init; }
    /// <summary>Zone reference id this service point belongs to.</summary>
    public int? ZoneId { get; init; }
    /// <summary>Secondary/backup contact phone number.</summary>
    public string? YedekTelefon { get; init; }
    /// <summary>Legacy numeric identifier carried from the source system.</summary>
    public int? Idd { get; init; }
    /// <summary>Latitude of the service point's geographic location.</summary>
    public decimal? LatX { get; init; }
    /// <summary>Longitude of the service point's geographic location.</summary>
    public decimal? LongY { get; init; }
}

/// <summary>Lightweight service-point projection for pickers/dropdowns, carrying only id, name and address.</summary>
public record ServicePointSimpleDto
{
    /// <summary>Stable unique identifier of the service point.</summary>
    public Guid Id { get; init; }
    /// <summary>Display name of the service point.</summary>
    public string HizmetNoktasiAdi { get; init; } = string.Empty;
    /// <summary>Free-text street address of the service point.</summary>
    public string? Adres { get; init; }
}
