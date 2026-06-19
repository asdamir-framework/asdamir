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
/// Export service for generating Excel and PDF files
/// </summary>
public interface IExportService
{
    /// <summary>
    /// Export data to Excel file using ClosedXML
    /// </summary>
    /// <typeparam name="T">Data model type</typeparam>
    /// <param name="data">Collection of data to export</param>
    /// <param name="sheetName">Excel sheet name</param>
    /// <param name="fileName">Output file name (without extension)</param>
    /// <returns>Excel file as byte array</returns>
    Task<byte[]> ExportToExcelAsync<T>(IEnumerable<T> data, string sheetName = "Data", string? fileName = null) where T : class;

    /// <summary>
    /// Export pre-projected column data to Excel using ClosedXML — the reusable primitive every
    /// column-aware export (e.g. <c>AsdamirGrid</c>) builds on: the caller supplies the exact header
    /// labels and per-row cell values it wants, so only the visible/selected columns are exported.
    /// </summary>
    /// <param name="headers">Column header labels, in order.</param>
    /// <param name="rows">One value list per row, aligned to <paramref name="headers"/>.</param>
    /// <param name="sheetName">Excel sheet name.</param>
    /// <returns>Excel file as byte array.</returns>
    Task<byte[]> ExportToExcelAsync(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<object?>> rows, string sheetName = "Data");

    /// <summary>Column-aware CSV export (UTF-8, quoted/escaped). Companion to the column-based Excel export.</summary>
    Task<byte[]> ExportToCsvAsync(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<object?>> rows);

    /// <summary>Column-aware PDF export (QuestPDF, landscape table). Companion to the column-based Excel export.</summary>
    Task<byte[]> ExportToPdfAsync(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<object?>> rows, string title = "Report");

    /// <summary>
    /// Export data to PDF file using QuestPDF
    /// </summary>
    /// <typeparam name="T">Data model type</typeparam>
    /// <param name="data">Collection of data to export</param>
    /// <param name="title">PDF document title</param>
    /// <param name="fileName">Output file name (without extension)</param>
    /// <returns>PDF file as byte array</returns>
    Task<byte[]> ExportToPdfAsync<T>(IEnumerable<T> data, string title = "Report", string? fileName = null) where T : class;

    /// <summary>
    /// Export data to CSV file
    /// </summary>
    /// <typeparam name="T">Data model type</typeparam>
    /// <param name="data">Collection of data to export</param>
    /// <param name="fileName">Output file name (without extension)</param>
    /// <returns>CSV file as byte array</returns>
    Task<byte[]> ExportToCsvAsync<T>(IEnumerable<T> data, string? fileName = null) where T : class;
}
