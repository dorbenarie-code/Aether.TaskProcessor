using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Aether.Application.Scheduling.ManagerConstraints;
using Aether.Domain.Optimization;
using Aether.Infrastructure.Scheduling.ScheduleGeneration;
using Microsoft.Win32;

namespace Aether.WpfApp;

public partial class MainWindow : Window
{
    private readonly LocalCleanXlsxScheduleGenerationRunner scheduleGenerationRunner;
    private readonly LocalCleanXlsxScheduleInputPreviewer scheduleInputPreviewer;

    private string? selectedInputWorkbookPath;
    private string? selectedOutputDirectoryPath;
    private string? generatedScheduleTableXlsxPath;
    private string? generatedOutputDirectoryPath;

    public ObservableCollection<ManagerConstraintDraftRow> ManagerConstraintDraftRows { get; } = [];

    public ObservableCollection<string> WorkerNameOptions { get; } = [];

    public ObservableCollection<DateTime> ScheduleDateOptions { get; } = [];

    public IReadOnlyList<ManagerConstraintDraftType> ManagerConstraintTypes { get; } =
        Enum.GetValues<ManagerConstraintDraftType>();

    public IReadOnlyList<ShiftKind> ShiftKinds { get; } =
        Enum.GetValues<ShiftKind>();

    public MainWindow()
        : this(
            new LocalCleanXlsxScheduleGenerationRunner(),
            new LocalCleanXlsxScheduleInputPreviewer())
    {
    }

    public MainWindow(
        LocalCleanXlsxScheduleGenerationRunner scheduleGenerationRunner)
        : this(
            scheduleGenerationRunner,
            new LocalCleanXlsxScheduleInputPreviewer())
    {
    }

    public MainWindow(
        LocalCleanXlsxScheduleGenerationRunner scheduleGenerationRunner,
        LocalCleanXlsxScheduleInputPreviewer scheduleInputPreviewer)
    {
        this.scheduleGenerationRunner = scheduleGenerationRunner ??
            throw new ArgumentNullException(nameof(scheduleGenerationRunner));

        this.scheduleInputPreviewer = scheduleInputPreviewer ??
            throw new ArgumentNullException(nameof(scheduleInputPreviewer));

        InitializeComponent();
        DataContext = this;
        RefreshGenerateButtonState();
    }

    private void SelectInputWorkbookButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "בחר קובץ בקשות עובדים",
            Filter = "Excel Workbook (*.xlsx)|*.xlsx|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        selectedInputWorkbookPath = dialog.FileName;
        InputWorkbookPathTextBlock.Text = selectedInputWorkbookPath;

        ClearManagerConstraintPickerOptions();

        try
        {
            var previewResult = scheduleInputPreviewer.Load(
                new LocalCleanXlsxScheduleInputPreviewRequest(selectedInputWorkbookPath));

            if (previewResult.Succeeded)
            {
                RefreshManagerConstraintPickerOptions(previewResult);
            }

            StatusTextBlock.Text = previewResult.Message;
        }
        catch (Exception exception)
        {
            ClearManagerConstraintPickerOptions();
            StatusTextBlock.Text = $"נכשלה טעינת תצוגה מקדימה של קובץ הבקשות: {exception.Message}";
        }

        RefreshGenerateButtonState();
    }


    private void SelectOutputDirectoryButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "בחר תיקיית שמירה"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        selectedOutputDirectoryPath = dialog.FolderName;
        OutputDirectoryPathTextBlock.Text = selectedOutputDirectoryPath;
        StatusTextBlock.Text = "תיקיית פלט נבחרה.";
        RefreshGenerateButtonState();
    }

    private async void GenerateScheduleButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(selectedInputWorkbookPath))
        {
            StatusTextBlock.Text = "נדרש לבחור קובץ בקשות עובדים.";
            return;
        }

        if (string.IsNullOrWhiteSpace(selectedOutputDirectoryPath))
        {
            StatusTextBlock.Text = "נדרש לבחור תיקיית שמירה.";
            return;
        }

        SetRunControlsEnabled(false);
        ClearGeneratedOutputState();
        GenerationProgressBar.Visibility = Visibility.Visible;
        StatusTextBlock.Text = "מחשב את הסידור האופטימלי... נא לא לסגור את החלון.";

        try
        {
            var applyPostRunLocalAddImprovement =
                ApplyPostRunLocalAddImprovementCheckBox.IsChecked == true;

            var manualManagerConstraintRows = new ManagerConstraintRowsBuilder()
                .Build(ManagerConstraintDraftRows);

            var result = await Task.Run(() =>
                scheduleGenerationRunner.Run(
                    new LocalCleanXlsxScheduleGenerationRequest(
                        selectedInputWorkbookPath,
                        selectedOutputDirectoryPath,
                        applyPostRunLocalAddImprovement,
                        ManualManagerConstraintRows: manualManagerConstraintRows)));

            StatusTextBlock.Text = result.Message;

            if (!result.Succeeded)
            {
                ClearGeneratedOutputState();
                return;
            }

            generatedScheduleTableXlsxPath = result.ScheduleTableXlsxPath;
            generatedOutputDirectoryPath = Path.GetDirectoryName(generatedScheduleTableXlsxPath);

            OpenScheduleTableXlsxButton.IsEnabled =
                !string.IsNullOrWhiteSpace(generatedScheduleTableXlsxPath);

            OpenOutputDirectoryButton.IsEnabled =
                !string.IsNullOrWhiteSpace(generatedOutputDirectoryPath);
        }
        catch (Exception exception)
        {
            ClearGeneratedOutputState();
            StatusTextBlock.Text = $"נכשל: {exception.Message}";
        }
        finally
        {
            GenerationProgressBar.Visibility = Visibility.Collapsed;
            SetRunControlsEnabled(true);
        }
    }

    private void OpenScheduleTableXlsxButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(generatedScheduleTableXlsxPath))
        {
            return;
        }

        OpenPath(generatedScheduleTableXlsxPath);
    }

    private void OpenOutputDirectoryButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(generatedOutputDirectoryPath))
        {
            return;
        }

        OpenPath(generatedOutputDirectoryPath);
    }

    private void ClearGeneratedOutputState()
    {
        generatedScheduleTableXlsxPath = null;
        generatedOutputDirectoryPath = null;
        OpenScheduleTableXlsxButton.IsEnabled = false;
        OpenOutputDirectoryButton.IsEnabled = false;
    }

    private void ClearManagerConstraintPickerOptions()
    {
        WorkerNameOptions.Clear();
        ScheduleDateOptions.Clear();
    }

    private void RefreshManagerConstraintPickerOptions(
        LocalCleanXlsxScheduleInputPreviewResult previewResult)
    {
        ClearManagerConstraintPickerOptions();

        foreach (var workerName in previewResult.WorkerNames)
        {
            WorkerNameOptions.Add(workerName);
        }

        foreach (var scheduleDate in previewResult.ScheduleDates)
        {
            ScheduleDateOptions.Add(scheduleDate.ToDateTime(TimeOnly.MinValue));
        }
    }

    private void SetRunControlsEnabled(bool isEnabled)
    {
        SelectInputWorkbookButton.IsEnabled = isEnabled;
        SelectOutputDirectoryButton.IsEnabled = isEnabled;
        ApplyPostRunLocalAddImprovementCheckBox.IsEnabled = isEnabled;
        ManagerConstraintsGrid.IsEnabled = isEnabled;

        GenerateScheduleButton.IsEnabled =
            isEnabled && HasRequiredGenerationInputs();
    }

    private void RefreshGenerateButtonState()
    {
        GenerateScheduleButton.IsEnabled = HasRequiredGenerationInputs();
    }

    private bool HasRequiredGenerationInputs()
    {
        return !string.IsNullOrWhiteSpace(selectedInputWorkbookPath) &&
               !string.IsNullOrWhiteSpace(selectedOutputDirectoryPath);
    }

    private static void OpenPath(string path)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }
}
