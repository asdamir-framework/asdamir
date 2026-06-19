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

public record ServicePointDto
{
    public Guid Id { get; init; }
    public Guid FirmaId { get; init; }
    public string HizmetNoktasiAdi { get; init; } = string.Empty;
    public bool Aktif { get; init; }
    public string? Adres { get; init; }
    public int? IlId { get; init; }
    public int? IlceId { get; init; }
    public int? SubeIdd { get; init; }
    public string? SubeAdi { get; init; }
    public int? BolgeIdd { get; init; }
    public string? BolgeAdi { get; init; }
    public string? ZoneAdi { get; init; }
    public string? YetkiliAdi { get; init; }
    public string? Telefon { get; init; }
    public DateTime? KayitTarihi { get; init; }
    public DateTime? CreatedAt { get; init; }
    public string? CreatedBy { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public string? UpdatedBy { get; init; }
    public int? FhnSiraNo { get; init; }
    public int? UlkeId { get; init; }
    public int? ZoneId { get; init; }
    public string? YedekTelefon { get; init; }
    public int? Idd { get; init; }
    public decimal? LatX { get; init; }
    public decimal? LongY { get; init; }
}

public record ServicePointSimpleDto
{
    public Guid Id { get; init; }
    public string HizmetNoktasiAdi { get; init; } = string.Empty;
    public string? Adres { get; init; }
}
