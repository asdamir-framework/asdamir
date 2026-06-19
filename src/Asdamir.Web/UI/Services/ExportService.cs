// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.ComponentModel;
using System.Reflection;
using System.Text;

namespace Asdamir.Web.UI.Services;

/// <summary>
/// Enterprise export service implementation
/// Uses: ClosedXML for Excel, QuestPDF for PDF
/// </summary>
public sealed class ExportService : IExportService
{
    public ExportService()
    {
        // QuestPDF license configuration (Community license - free for non-commercial)
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>
    /// Export to Excel using ClosedXML with advanced formatting
    /// Features: Header styling, auto-filter, freeze panes, alternating rows, borders
    /// </summary>
    public async Task<byte[]> ExportToExcelAsync<T>(IEnumerable<T> data, string sheetName = "Data", string? fileName = null) where T : class
    {
        return await Task.Run(() =>
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add(sheetName);

            var properties = typeof(T).GetProperties();
            var dataList = data.ToList();

            // HEADER ROW - Professional styling
            for (int i = 0; i < properties.Length; i++)
            {
                var displayName = GetDisplayName(properties[i]);
                var headerCell = worksheet.Cell(1, i + 1);

                headerCell.Value = displayName;
                headerCell.Style.Font.Bold = true;
                headerCell.Style.Font.FontSize = 11;
                headerCell.Style.Font.FontColor = XLColor.White;
                headerCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#0066CC"); // Enterprise blue
                headerCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                headerCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                headerCell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            // DATA ROWS - With alternating colors and borders
            for (int row = 0; row < dataList.Count; row++)
            {
                var item = dataList[row];
                var isEvenRow = row % 2 == 0;

                for (int col = 0; col < properties.Length; col++)
                {
                    var value = properties[col].GetValue(item);
                    var cell = worksheet.Cell(row + 2, col + 1);

                    // Set value
                    cell.Value = value?.ToString() ?? "";

                    // Alternating row colors
                    if (isEvenRow)
                    {
                        cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#F8F9FA");
                    }

                    // Borders
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#DEE2E6");

                    // Alignment based on data type
                    var propertyType = properties[col].PropertyType;
                    if (IsNumericType(propertyType))
                    {
                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                        // Format numbers with thousands separator
                        if (value is decimal || value is double || value is float)
                        {
                            cell.Style.NumberFormat.Format = "#,##0.00";
                        }
                        else if (value is int || value is long)
                        {
                            cell.Style.NumberFormat.Format = "#,##0";
                        }
                    }
                    else if (propertyType == typeof(DateTime) || propertyType == typeof(DateTime?))
                    {
                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        if (value is DateTime dateValue)
                        {
                            cell.Value = dateValue;
                            cell.Style.DateFormat.Format = "yyyy-MM-dd HH:mm";
                        }
                    }
                    else
                    {
                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                    }
                }
            }

            // AUTO-FIT COLUMNS (with max width limit)
            foreach (var column in worksheet.ColumnsUsed())
            {
                column.AdjustToContents(1, 1, 5, 50); // Min 5, Max 50
            }

            // FREEZE TOP ROW (Header stays visible when scrolling)
            worksheet.SheetView.FreezeRows(1);

            // AUTO-FILTER (Excel filter dropdowns on headers)
            var dataRange = worksheet.Range(1, 1, dataList.Count + 1, properties.Length);
            dataRange.SetAutoFilter();

            // ADD SUMMARY ROW (Optional - if there are numeric columns)
            var hasNumericColumns = properties.Any(p => IsNumericType(p.PropertyType));
            if (hasNumericColumns && dataList.Count > 0)
            {
                var summaryRow = dataList.Count + 2;
                worksheet.Cell(summaryRow, 1).Value = "TOTAL";
                worksheet.Cell(summaryRow, 1).Style.Font.Bold = true;

                for (int col = 0; col < properties.Length; col++)
                {
                    var cell = worksheet.Cell(summaryRow, col + 1);
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E9ECEF");
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                    if (IsNumericType(properties[col].PropertyType))
                    {
                        // Sum formula
                        var startCell = worksheet.Cell(2, col + 1).Address.ToString();
                        var endCell = worksheet.Cell(dataList.Count + 1, col + 1).Address.ToString();
                        cell.FormulaA1 = $"SUM({startCell}:{endCell})";
                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                        if (properties[col].PropertyType == typeof(decimal) ||
                            properties[col].PropertyType == typeof(decimal?) ||
                            properties[col].PropertyType == typeof(double) ||
                            properties[col].PropertyType == typeof(double?) ||
                            properties[col].PropertyType == typeof(float) ||
                            properties[col].PropertyType == typeof(float?))
                        {
                            cell.Style.NumberFormat.Format = "#,##0.00";
                        }
                        else
                        {
                            cell.Style.NumberFormat.Format = "#,##0";
                        }
                    }
                }
            }

            // DOCUMENT PROPERTIES
            workbook.Properties.Author = "Enterprise Framework";
            workbook.Properties.Company = "Your Company";
            workbook.Properties.Created = DateTime.Now;
            workbook.Properties.Title = sheetName;

            // Convert to byte array
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        });
    }

    /// <summary>
    /// Export pre-projected column data to Excel (shared primitive for column-aware grids).
    /// Same professional styling as the typed overload: header band, alternating rows, borders,
    /// value-type-aware alignment/formatting, auto-fit, freeze header and auto-filter.
    /// </summary>
    public async Task<byte[]> ExportToExcelAsync(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<object?>> rows, string sheetName = "Data")
    {
        return await Task.Run(() =>
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add(sheetName);
            var rowList = rows.ToList();

            for (int i = 0; i < headers.Count; i++)
            {
                var headerCell = worksheet.Cell(1, i + 1);
                headerCell.Value = headers[i];
                headerCell.Style.Font.Bold = true;
                headerCell.Style.Font.FontSize = 11;
                headerCell.Style.Font.FontColor = XLColor.White;
                headerCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#0066CC");
                headerCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                headerCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                headerCell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            for (int row = 0; row < rowList.Count; row++)
            {
                var values = rowList[row];
                var isEvenRow = row % 2 == 0;

                for (int col = 0; col < headers.Count; col++)
                {
                    var value = col < values.Count ? values[col] : null;
                    var cell = worksheet.Cell(row + 2, col + 1);

                    switch (value)
                    {
                        case null:
                            cell.Value = "";
                            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                            break;
                        case DateTime dt:
                            cell.Value = dt;
                            cell.Style.DateFormat.Format = "yyyy-MM-dd HH:mm";
                            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                            break;
                        case decimal or double or float:
                            cell.Value = Convert.ToDouble(value);
                            cell.Style.NumberFormat.Format = "#,##0.00";
                            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                            break;
                        case int or long or short or byte:
                            cell.Value = Convert.ToInt64(value);
                            cell.Style.NumberFormat.Format = "#,##0";
                            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                            break;
                        case bool b:
                            cell.Value = b;
                            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                            break;
                        default:
                            cell.Value = value.ToString();
                            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                            break;
                    }

                    if (isEvenRow)
                        cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#F8F9FA");
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#DEE2E6");
                }
            }

            foreach (var column in worksheet.ColumnsUsed())
                column.AdjustToContents(1, 1, 5, 60);

            if (headers.Count > 0)
            {
                worksheet.SheetView.FreezeRows(1);
                worksheet.Range(1, 1, rowList.Count + 1, headers.Count).SetAutoFilter();
            }

            workbook.Properties.Author = "Asdamir";
            workbook.Properties.Created = DateTime.Now;
            workbook.Properties.Title = sheetName;

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        });
    }

    /// <summary>Column-aware CSV export (UTF-8 with BOM, RFC-4180 quoting).</summary>
    public async Task<byte[]> ExportToCsvAsync(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<object?>> rows)
    {
        return await Task.Run(() =>
        {
            static string Cell(object? v) => "\"" + (v switch
            {
                null => "",
                DateTime dt => dt.ToString("yyyy-MM-dd HH:mm"),
                _ => v.ToString() ?? ""
            }).Replace("\"", "\"\"") + "\"";

            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", headers.Select(h => Cell(h))));
            foreach (var row in rows)
                sb.AppendLine(string.Join(",", row.Select(Cell)));

            // UTF-8 BOM so Excel opens Turkish/Cyrillic characters correctly.
            return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        });
    }

    /// <summary>Column-aware PDF export (QuestPDF, landscape table with a header band).</summary>
    public async Task<byte[]> ExportToPdfAsync(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<object?>> rows, string title = "Report")
    {
        return await Task.Run(() =>
        {
            var rowList = rows.ToList();
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1.5f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(8));

                    page.Header().Text(title).SemiBold().FontSize(16).FontColor(Colors.Blue.Medium);

                    page.Content().PaddingVertical(0.5f, Unit.Centimetre).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            foreach (var _ in headers) columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            foreach (var h in headers)
                                header.Cell().Background(Colors.Blue.Medium).Padding(4)
                                      .Text(h).FontColor(Colors.White).SemiBold();
                        });

                        foreach (var row in rowList)
                        {
                            for (int c = 0; c < headers.Count; c++)
                            {
                                var v = c < row.Count ? row[c] : null;
                                var text = v switch { null => "", DateTime dt => dt.ToString("yyyy-MM-dd HH:mm"), _ => v.ToString() ?? "" };
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(text);
                            }
                        }
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Sayfa "); x.CurrentPageNumber(); x.Span(" / "); x.TotalPages();
                    });
                });
            });
            return document.GeneratePdf();
        });
    }

    /// <summary>
    /// Check if type is numeric
    /// </summary>
    private static bool IsNumericType(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        return underlyingType == typeof(int) ||
               underlyingType == typeof(long) ||
               underlyingType == typeof(short) ||
               underlyingType == typeof(byte) ||
               underlyingType == typeof(decimal) ||
               underlyingType == typeof(double) ||
               underlyingType == typeof(float);
    }

    /// <summary>
    /// Export to PDF using QuestPDF
    /// </summary>
    public async Task<byte[]> ExportToPdfAsync<T>(IEnumerable<T> data, string title = "Report", string? fileName = null) where T : class
    {
        return await Task.Run(() =>
        {
            var properties = typeof(T).GetProperties();
            var dataList = data.ToList();

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    // Header
                    page.Header()
                        .Text(title)
                        .SemiBold().FontSize(20).FontColor(Colors.Blue.Medium);

                    // Content
                    page.Content()
                        .PaddingVertical(1, Unit.Centimetre)
                        .Table(table =>
                        {
                            // Columns
                            table.ColumnsDefinition(columns =>
                            {
                                foreach (var prop in properties)
                                {
                                    columns.RelativeColumn();
                                }
                            });

                            // Header
                            table.Header(header =>
                            {
                                foreach (var prop in properties)
                                {
                                    header.Cell().Element(CellStyle).Text(GetDisplayName(prop)).SemiBold();
                                }

                                QuestPDF.Infrastructure.IContainer CellStyle(QuestPDF.Infrastructure.IContainer container)
                                {
                                    return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5);
                                }
                            });

                            // Rows
                            foreach (var item in dataList)
                            {
                                foreach (var prop in properties)
                                {
                                    var value = prop.GetValue(item);
                                    table.Cell().Element(CellStyle).Text(value?.ToString() ?? "");
                                }

                                QuestPDF.Infrastructure.IContainer CellStyle(QuestPDF.Infrastructure.IContainer container)
                                {
                                    return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(5);
                                }
                            }
                        });

                    // Footer
                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Page ");
                            x.CurrentPageNumber();
                            x.Span(" of ");
                            x.TotalPages();
                        });
                });
            });

            return document.GeneratePdf();
        });
    }

    /// <summary>
    /// Export to CSV (simple, no dependencies)
    /// </summary>
    public async Task<byte[]> ExportToCsvAsync<T>(IEnumerable<T> data, string? fileName = null) where T : class
    {
        return await Task.Run(() =>
        {
            var properties = typeof(T).GetProperties();
            var sb = new StringBuilder();

            // Header
            sb.AppendLine(string.Join(",", properties.Select(p => $"\"{GetDisplayName(p)}\"")));

            // Rows
            foreach (var item in data)
            {
                var values = properties.Select(p =>
                {
                    var value = p.GetValue(item);
                    return $"\"{value?.ToString()?.Replace("\"", "\"\"")}\""; // Escape quotes
                });
                sb.AppendLine(string.Join(",", values));
            }

            return Encoding.UTF8.GetBytes(sb.ToString());
        });
    }

    /// <summary>
    /// Get display name from DisplayName attribute or property name
    /// </summary>
    private static string GetDisplayName(PropertyInfo property)
    {
        var displayAttribute = property.GetCustomAttribute<DisplayNameAttribute>();
        return displayAttribute?.DisplayName ?? property.Name;
    }
}
