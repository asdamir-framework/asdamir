// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using System.Globalization;

namespace Asdamir.Core.Scheduling;

/// <summary>
/// A minimal, dependency-free 5-field cron evaluator: <c>minute hour day-of-month month day-of-week</c>.
/// Used for the scheduler UI's "next run" preview without taking a NuGet dependency (e.g. Cronos) —
/// the framework keeps Core dependency-light on purpose.
///
/// <para>Supported per field: <c>*</c>, a single value, lists (<c>1,2,3</c>), ranges (<c>1-5</c>),
/// and steps (<c>*/5</c>, <c>10-30/5</c>). Day-of-week is 0–6 with Sunday = 0 (7 also accepted as
/// Sunday). When BOTH day-of-month and day-of-week are restricted, a match on EITHER counts — the
/// standard Vixie-cron rule. All evaluation is in the supplied <see cref="DateTime"/>'s own clock
/// (pass UTC for UTC schedules).</para>
///
/// This is intentionally not a full Quartz/Cronos replacement (no seconds, <c>L</c>/<c>W</c>/<c>#</c>,
/// or named months/days) — just enough for recurring-job previews.
/// </summary>
public sealed class CronSchedule
{
    private readonly bool[] _minutes;   // 0-59
    private readonly bool[] _hours;     // 0-23
    private readonly bool[] _daysOfMonth; // 1-31
    private readonly bool[] _months;    // 1-12
    private readonly bool[] _daysOfWeek; // 0-6 (Sun=0)
    private readonly bool _domRestricted;
    private readonly bool _dowRestricted;

    private CronSchedule(bool[] minutes, bool[] hours, bool[] daysOfMonth, bool[] months, bool[] daysOfWeek,
        bool domRestricted, bool dowRestricted)
    {
        _minutes = minutes;
        _hours = hours;
        _daysOfMonth = daysOfMonth;
        _months = months;
        _daysOfWeek = daysOfWeek;
        _domRestricted = domRestricted;
        _dowRestricted = dowRestricted;
    }

    /// <summary>Parses a 5-field cron expression, or throws <see cref="FormatException"/>.</summary>
    public static CronSchedule Parse(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            throw new FormatException("Cron expression is empty.");

        var fields = expression.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length != 5)
            throw new FormatException($"Expected 5 cron fields (minute hour day month weekday), got {fields.Length}.");

        var minutes = ParseField(fields[0], 0, 59);
        var hours = ParseField(fields[1], 0, 23);
        var dom = ParseField(fields[2], 1, 31);
        var months = ParseField(fields[3], 1, 12);
        var dow = ParseDayOfWeek(fields[4]);

        return new CronSchedule(minutes, hours, dom, months, dow,
            domRestricted: fields[2] != "*", dowRestricted: fields[4] != "*");
    }

    /// <summary>Returns the parsed schedule, or <c>null</c> if <paramref name="expression"/> is invalid.</summary>
    public static CronSchedule? TryParse(string expression)
    {
        try { return Parse(expression); }
        catch (FormatException) { return null; }
    }

    /// <summary>
    /// The next occurrence strictly after <paramref name="after"/> (truncated to the minute), or
    /// <c>null</c> if none within ~4 years (e.g. an impossible date like Feb 30).
    /// </summary>
    public DateTime? GetNextOccurrence(DateTime after)
    {
        // Start at the next whole minute after `after`.
        var t = new DateTime(after.Year, after.Month, after.Day, after.Hour, after.Minute, 0, after.Kind)
            .AddMinutes(1);

        // Bound the search so a never-matching expression can't loop forever (~4 years of minutes).
        var limit = t.AddYears(4);
        while (t < limit)
        {
            if (!_months[t.Month])
            {
                // Jump to the first day of the next month at 00:00.
                t = new DateTime(t.Year, t.Month, 1, 0, 0, 0, t.Kind).AddMonths(1);
                continue;
            }
            if (!DayMatches(t))
            {
                t = new DateTime(t.Year, t.Month, t.Day, 0, 0, 0, t.Kind).AddDays(1);
                continue;
            }
            if (!_hours[t.Hour])
            {
                t = new DateTime(t.Year, t.Month, t.Day, t.Hour, 0, 0, t.Kind).AddHours(1);
                continue;
            }
            if (!_minutes[t.Minute])
            {
                t = t.AddMinutes(1);
                continue;
            }
            return t;
        }
        return null;
    }

    private bool DayMatches(DateTime t)
    {
        var domOk = _daysOfMonth[t.Day];
        var dowOk = _daysOfWeek[(int)t.DayOfWeek];

        // Vixie-cron: if both DOM and DOW are restricted, either matching is enough; if only one is
        // restricted, that one must match; if neither, any day matches.
        if (_domRestricted && _dowRestricted) return domOk || dowOk;
        if (_domRestricted) return domOk;
        if (_dowRestricted) return dowOk;
        return true;
    }

    private static bool[] ParseField(string field, int min, int max)
    {
        var set = new bool[max + 1];
        foreach (var part in field.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            // Optional step: "<range>/<step>".
            var step = 1;
            var rangePart = part;
            var slash = part.IndexOf('/');
            if (slash >= 0)
            {
                rangePart = part[..slash];
                step = ParseInt(part[(slash + 1)..], "step");
                if (step <= 0) throw new FormatException($"Cron step must be positive: '{part}'.");
            }

            int lo, hi;
            if (rangePart == "*")
            {
                lo = min; hi = max;
            }
            else if (rangePart.Contains('-'))
            {
                var bounds = rangePart.Split('-');
                if (bounds.Length != 2) throw new FormatException($"Invalid cron range: '{rangePart}'.");
                lo = ParseInt(bounds[0], "value");
                hi = ParseInt(bounds[1], "value");
            }
            else
            {
                lo = hi = ParseInt(rangePart, "value");
            }

            if (lo < min || hi > max || lo > hi)
                throw new FormatException($"Cron value out of range [{min}-{max}]: '{part}'.");

            for (var v = lo; v <= hi; v += step) set[v] = true;
        }
        return set;
    }

    private static bool[] ParseDayOfWeek(string field)
    {
        // Accept 7 as Sunday by normalizing to 0 before parsing into the 0-6 set.
        var normalized = field.Replace("7", "0");
        return ParseField(normalized, 0, 6);
    }

    private static int ParseInt(string s, string what)
        => int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out var v)
            ? v
            : throw new FormatException($"Invalid cron {what}: '{s}'.");
}
