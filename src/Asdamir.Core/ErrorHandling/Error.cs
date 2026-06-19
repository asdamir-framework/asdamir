// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Core.ErrorHandling.Abstractions;

/// <summary>
/// 3 dilli hata modeli. Her dil için ayrı mesaj içerir.
/// </summary>
public sealed record Error(
	string Code,
	string MessageTr,
	string MessageEn,
	string MessageRu
)
{
	/// <summary>
	/// Aktif dile göre uygun mesajı döner. (Varsayılan: Türkçe)
	/// </summary>
	public string GetMessage(string? lang)
	{
		return lang switch
		{
			"en" => MessageEn,
			"ru" => MessageRu,
			_ => MessageTr
		};
	}
}
