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

public record CompanyDto
{
    public Guid Id { get; init; }
    public string SubeliFirma { get; init; } = string.Empty;
    public string KisaAdi { get; init; } = string.Empty;
    public string Unvani { get; init; } = string.Empty;
    public string? MuhasebeKodu { get; init; }
    public string? VergiDairesi { get; init; }
    public string? VergiNo { get; init; }
    public bool Aktif { get; init; }
    public string? Adres { get; init; }
    public int? IlId { get; init; }
    public int? IlceId { get; init; }
    public int? SubeIdd { get; init; }
    public string? SubeAdi { get; init; }
    public int? BolgeIdd { get; init; }
    public string? BolgeAdi { get; init; }
    public DateTime? SozlesmeBaslangici { get; init; }
    public bool Deleted { get; init; }
    public string? OperasyonBolgeAdi { get; init; }
    public int? OperasyonBolgeId { get; init; }
}

public record CompanySimpleDto
{
    public Guid Id { get; init; }
    public string KisaAdi { get; init; } = string.Empty;
    public string Unvani { get; init; } = string.Empty;
}
