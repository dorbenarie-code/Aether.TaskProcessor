using System.Text;
using Aether.Application.Scheduling.ManagerConstraints;
using Aether.Application.Scheduling.SubmissionForms;
using Aether.Application.Scheduling.Profiles;
using Aether.Application.Scheduling.ScheduleGeneration;
using Aether.Domain.Optimization;

namespace Aether.Infrastructure.Scheduling.ScheduleGeneration;

public sealed class LocalCleanXlsxScheduleGenerationRunner
{
    private readonly ILocalCleanXlsxWorkbookInputReader workbookInputReader;
    private readonly ILocalCleanXlsxScheduleGenerator scheduleGenerator;

    public LocalCleanXlsxScheduleGenerationRunner()
        : this(
            new LocalCleanXlsxWorkbookInputReaderAdapter(),
            new LocalCleanXlsxScheduleGenerator())
    {
    }

    public LocalCleanXlsxScheduleGenerationRunner(
        ILocalCleanXlsxWorkbookInputReader workbookInputReader,
        ILocalCleanXlsxScheduleGenerator scheduleGenerator)
    {
        this.workbookInputReader = workbookInputReader ??
            throw new ArgumentNullException(nameof(workbookInputReader));

        this.scheduleGenerator = scheduleGenerator ??
            throw new ArgumentNullException(nameof(scheduleGenerator));
    }

    public LocalCleanXlsxScheduleGenerationResult Run(
        LocalCleanXlsxScheduleGenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!File.Exists(request.InputWorkbookPath))
        {
            return new LocalCleanXlsxScheduleGenerationResult(
                Succeeded: false,
                FailureType: LocalCleanXlsxScheduleGenerationFailureType.InputWorkbookNotFound,
                Message: $"Input workbook was not found: {request.InputWorkbookPath}");
        }

        Directory.CreateDirectory(request.OutputDirectoryPath);

        using var workbookInput = workbookInputReader.Open(
            request.InputWorkbookPath);

        var profile = new LastDorLocalScheduleGenerationProfile();
        var schedulePeriod = profile.CreateSchedulePeriod();

        var generationResult = scheduleGenerator.Run(
            new CleanXlsxScheduleGenerationRequest(
                workbookInput.AvailabilityMatrixStream,
                schedulePeriod,
                profile.CreateResources(),
                profile.CreateShifts(),
                profile.TotalEffectiveTargetHours,
                MaximumAssignedHoursDeviationFromAverageHours:
                    profile.MaximumAssignedHoursDeviationFromAverageHours,
                Seed: profile.Seed,
                ManagerConstraintRows: CombineManagerConstraintRows(
                    workbookInput.ManagerConstraintRows,
                    request.ManualManagerConstraintRows),
                ApplyPostRunLocalAddImprovement:
                    request.ApplyPostRunLocalAddImprovement,
                IncludeTargetGapDiagnostics: false));

        if (!generationResult.Succeeded)
        {
            return new LocalCleanXlsxScheduleGenerationResult(
                Succeeded: false,
                FailureType: LocalCleanXlsxScheduleGenerationFailureType.GenerationFailed,
                Message: CreateGenerationFailureMessage(generationResult));
        }

        if (generationResult.ScheduleTableXlsxBytes is null)
        {
            return new LocalCleanXlsxScheduleGenerationResult(
                Succeeded: false,
                FailureType: LocalCleanXlsxScheduleGenerationFailureType.GenerationFailed,
                Message: "Schedule generation completed without XLSX bytes.");
        }

        var scheduleTableXlsxPath = Path.Combine(
            request.OutputDirectoryPath,
            CreateScheduleTableXlsxFileName(schedulePeriod));

        File.WriteAllBytes(
            scheduleTableXlsxPath,
            generationResult.ScheduleTableXlsxBytes);

        return new LocalCleanXlsxScheduleGenerationResult(
            Succeeded: true,
            FailureType: LocalCleanXlsxScheduleGenerationFailureType.None,
            Message: $"Schedule generation completed successfully: {scheduleTableXlsxPath}",
            ScheduleTableXlsxPath: scheduleTableXlsxPath);
    }


    private static IReadOnlyList<IReadOnlyList<string>>? CombineManagerConstraintRows(
        IReadOnlyList<IReadOnlyList<string>>? workbookRows,
        IReadOnlyList<IReadOnlyList<string>>? manualRows)
    {
        if (workbookRows is null)
        {
            return manualRows;
        }

        if (manualRows is null)
        {
            return workbookRows;
        }

        var combinedRows = new List<IReadOnlyList<string>>();

        if (workbookRows.Count > 0)
        {
            combinedRows.Add(workbookRows[0]);
            AppendDataRows(combinedRows, workbookRows);
        }
        else if (manualRows.Count > 0)
        {
            combinedRows.Add(manualRows[0]);
        }

        AppendDataRows(combinedRows, manualRows);

        return combinedRows.Count == 0
            ? null
            : combinedRows;
    }

    private static void AppendDataRows(
        ICollection<IReadOnlyList<string>> target,
        IReadOnlyList<IReadOnlyList<string>> source)
    {
        foreach (var row in source.Skip(1))
        {
            target.Add(row);
        }
    }


    private static string CreateScheduleTableXlsxFileName(
        SchedulePeriod schedulePeriod)
    {
        return $"last-dor-schedule-{schedulePeriod.StartUtc:yyyy-MM-dd}-to-{schedulePeriod.EndUtc:yyyy-MM-dd}.xlsx";
    }


    private static string CreateGenerationFailureMessage(
        CleanXlsxScheduleGenerationResult generationResult)
    {
        return generationResult.FailureType switch
        {
            ScheduleGenerationFailureType.AvailabilityMatrixImportFailed =>
                CreateWorkerRequestsImportFailureMessage(generationResult),

            ScheduleGenerationFailureType.ManagerConstraintImportFailed =>
                CreateManagerConstraintImportFailureMessage(generationResult),

            ScheduleGenerationFailureType.OptimizationResultMissing =>
                "לא ניתן לייצר סידור עבודה.\n\nהמערכת לא הצליחה להרכיב סידור תקין. בדוק שקובץ בקשות העובדים ואילוצי המנהל תקינים ונסה שוב.",

            _ =>
                "לא ניתן לייצר סידור עבודה.\n\nאירעה תקלה ביצירת הסידור. בדוק את קובץ בקשות העובדים ונסה שוב."
        };
    }


    private static string CreateWorkerRequestsImportFailureMessage(
        CleanXlsxScheduleGenerationResult generationResult)
    {
        var builder = new StringBuilder();

        builder.AppendLine("לא ניתן לייצר סידור עבודה.");
        builder.AppendLine();
        builder.AppendLine("נמצאה בעיה בקובץ בקשות העובדים:");

        if (generationResult.ImportFatalErrors.Count == 0)
        {
            builder.AppendLine(
                "- לא ניתן לקרוא את קובץ בקשות העובדים שנבחר. בדוק את הקובץ ונסה שוב.");

            return builder.ToString();
        }

        foreach (var fatalError in generationResult.ImportFatalErrors)
        {
            builder.AppendLine($"- {FormatWorkerRequestsFatalError(fatalError)}");
        }

        return builder.ToString();
    }


    private static string FormatWorkerRequestsFatalError(
        AvailabilityMatrixImportFatalError fatalError)
    {
        var location = FormatWorkerRequestsErrorLocation(fatalError);
        var rawValue = FormatRawValue(fatalError.RawValue);

        return fatalError.Type switch
        {
            AvailabilityMatrixImportFatalErrorType.EmptyTable =>
                $"{location}: קובץ בקשות העובדים ריק. יש לבחור קובץ בקשות עובדים תקין.",

            AvailabilityMatrixImportFatalErrorType.MissingWorkerNameColumn =>
                $"{location}: חסרה עמודת שם עובד בקובץ בקשות העובדים. בדוק שהכותרת קיימת ונכתבה נכון.",

            AvailabilityMatrixImportFatalErrorType.MissingScheduleDateColumn =>
                $"{location}: חסרה עמודת תאריך עבור תקופת הסידור. בדוק שכל תאריכי הסידור קיימים בקובץ.",

            AvailabilityMatrixImportFatalErrorType.DuplicateScheduleDateColumn =>
                $"{location}: קיימת עמודת תאריך כפולה בקובץ בקשות העובדים. יש להשאיר עמודה אחת בלבד לכל תאריך.",

            AvailabilityMatrixImportFatalErrorType.DuplicateWorkerRow =>
                $"{location}: העובד מופיע יותר מפעם אחת בקובץ בקשות העובדים.{rawValue} יש להשאיר שורה אחת לכל עובד.",

            AvailabilityMatrixImportFatalErrorType.UnresolvedWorkerName =>
                $"{location}: עובד לא נמצא ברשימת העובדים שהמערכת מכירה.{rawValue} יש לבדוק את שם העובד בקובץ או לבחור עובד קיים.",

            AvailabilityMatrixImportFatalErrorType.MissingWorkerNameForNonEmptyRow =>
                $"{location}: חסר שם עובד בשורה שיש בה נתונים. יש להשלים את שם העובד או למחוק את השורה.",

            _ =>
                $"{location}: שגיאה בקובץ בקשות העובדים.{rawValue} בדוק את השורה ונסה שוב."
        };
    }


    private static string FormatWorkerRequestsErrorLocation(
        AvailabilityMatrixImportFatalError fatalError)
    {
        var parts = new List<string>();

        if (fatalError.RowIndex.HasValue)
        {
            parts.Add($"שורה {fatalError.RowIndex.Value}");
        }

        if (!string.IsNullOrWhiteSpace(fatalError.Header))
        {
            parts.Add(FormatWorkerRequestsHeader(fatalError.Header));
        }
        else if (fatalError.ColumnIndex.HasValue)
        {
            parts.Add($"עמודה {fatalError.ColumnIndex.Value}");
        }

        if (fatalError.Date.HasValue)
        {
            parts.Add($"תאריך {fatalError.Date.Value:yyyy-MM-dd}");
        }

        return parts.Count == 0
            ? "מיקום לא ידוע"
            : string.Join(", ", parts);
    }


    private static string FormatWorkerRequestsHeader(string header)
    {
        return header switch
        {
            "WorkerName" => "שם עובד",
            "ResourceName" => "שם עובד",
            _ => header
        };
    }


    private static string CreateManagerConstraintImportFailureMessage(
        CleanXlsxScheduleGenerationResult generationResult)
    {
        var builder = new StringBuilder();

        builder.AppendLine("לא ניתן לייצר סידור עבודה.");
        builder.AppendLine();
        builder.AppendLine("נמצאה בעיה באילוצי המנהל:");

        if (generationResult.ManagerConstraintImportFatalErrors.Count == 0)
        {
            builder.AppendLine(
                "- לא ניתן לקרוא את אילוצי המנהל מהגיליון ManagerConstraints או מהטבלה בחלון. בדוק את הנתונים ונסה שוב.");

            return builder.ToString();
        }

        foreach (var fatalError in generationResult.ManagerConstraintImportFatalErrors)
        {
            builder.AppendLine($"- {FormatManagerConstraintFatalError(fatalError)}");
        }

        return builder.ToString();
    }


    private static string FormatManagerConstraintFatalError(
        ManagerConstraintRowsImportFatalError fatalError)
    {
        var location = FormatManagerConstraintErrorLocation(fatalError);
        var rawValue = FormatRawValue(fatalError.RawValue);

        return fatalError.Type switch
        {
            ManagerConstraintRowsImportFatalErrorType.EmptyTable =>
                $"{location}: טבלת אילוצי המנהל ריקה. יש להזין אילוץ תקין או להשאיר את הטבלה ריקה לגמרי.",

            ManagerConstraintRowsImportFatalErrorType.MissingRequiredColumn =>
                $"{location}: חסרה עמודה נדרשת באילוצי המנהל. בדוק שהכותרות קיימות ונכתבו נכון.",

            ManagerConstraintRowsImportFatalErrorType.UnknownConstraintType =>
                $"{location}: סוג אילוץ לא מוכר.{rawValue} יש לבחור סוג אילוץ מתוך הרשימה.",

            ManagerConstraintRowsImportFatalErrorType.MissingWorkerName =>
                $"{location}: חסר עובד. יש להשלים את שדה העובד או למחוק את השורה.",

            ManagerConstraintRowsImportFatalErrorType.UnresolvedWorkerName =>
                $"{location}: העובד לא נמצא ברשימת העובדים של הקובץ שנבחר.{rawValue} יש לבחור עובד מתוך הרשימה.",

            ManagerConstraintRowsImportFatalErrorType.InvalidDate =>
                $"{location}: התאריך לא תקין.{rawValue} יש לבחור תאריך מתוך הרשימה.",

            ManagerConstraintRowsImportFatalErrorType.DateOutsideSchedulePeriod =>
                $"{location}: התאריך מחוץ לתקופת הסידור. יש לבחור תאריך מתוך הרשימה.",

            ManagerConstraintRowsImportFatalErrorType.InvalidShiftKind =>
                $"{location}: המשמרת לא תקינה.{rawValue} יש לבחור משמרת מתוך הרשימה.",

            ManagerConstraintRowsImportFatalErrorType.ShiftNotFound =>
                $"{location}: לא נמצאה משמרת מתאימה לתאריך ולסוג המשמרת שנבחרו.",

            ManagerConstraintRowsImportFatalErrorType.AmbiguousShift =>
                $"{location}: נמצאו כמה משמרות שמתאימות לאותו תאריך וסוג משמרת. יש לבדוק את הגדרת המשמרות.",

            ManagerConstraintRowsImportFatalErrorType.MissingCapacityValue =>
                $"{location}: חסר ערך כמות עובדים. יש למלא מינימום ומקסימום.",

            ManagerConstraintRowsImportFatalErrorType.InvalidCapacityValue =>
                $"{location}: ערך כמות העובדים לא תקין.{rawValue} יש להזין מספרים תקינים.",

            ManagerConstraintRowsImportFatalErrorType.UnexpectedWorkerName =>
                $"{location}: באילוץ שינוי כמות עובדים אין לבחור עובד.{rawValue} נקה את שדה העובד ונסה שוב.",

            ManagerConstraintRowsImportFatalErrorType.UnexpectedCapacityValue =>
                $"{location}: באילוץ עובד אין למלא מינימום או מקסימום עובדים.{rawValue} נקה את שדות הכמות ונסה שוב.",

            _ =>
                $"{location}: אילוץ המנהל לא תקין.{rawValue} בדוק את השורה ונסה שוב."
        };
    }


    private static string FormatManagerConstraintErrorLocation(
        ManagerConstraintRowsImportFatalError fatalError)
    {
        var parts = new List<string>();

        if (fatalError.RowIndex.HasValue)
        {
            parts.Add($"שורה {fatalError.RowIndex.Value}");
        }

        if (!string.IsNullOrWhiteSpace(fatalError.Header))
        {
            parts.Add(FormatManagerConstraintHeader(fatalError.Header));
        }

        return parts.Count == 0
            ? "שורה לא ידועה"
            : string.Join(", ", parts);
    }


    private static string FormatManagerConstraintHeader(string header)
    {
        return header switch
        {
            "Type" => "סוג אילוץ",
            "WorkerName" => "עובד",
            "Date" => "תאריך",
            "ShiftKind" => "משמרת",
            "MinResourceCount" => "מינימום",
            "MaxResourceCount" => "מקסימום",
            _ => header
        };
    }


    private static string FormatRawValue(string? rawValue)
    {
        return string.IsNullOrWhiteSpace(rawValue)
            ? string.Empty
            : $" ערך: {rawValue}.";
    }

}
