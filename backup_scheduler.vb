Imports System.IO
Imports System.Threading.Tasks
Imports System.Windows.Forms

Public Class BackupForm
    Inherits Form

    Private ReadOnly txtSource As New TextBox()
    Private ReadOnly txtDestination As New TextBox()
    Private ReadOnly txtTime As New TextBox()
    Private ReadOnly btnStart As New Button()
    Private ReadOnly btnRunNow As New Button()
    Private ReadOnly lblStatus As New Label()
    Private ReadOnly lblLastRun As New Label()
    Private ReadOnly lblNextRun As New Label()
    Private ReadOnly progress As New ProgressBar()
    Private ReadOnly txtLog As New TextBox()
    Private ReadOnly scheduler As New Timer()

    Private backupTime As TimeSpan
    Private lastRunDate As Date = Date.MinValue
    Private isRunning As Boolean = False
    Private schedulerEnabled As Boolean = False

    Public Sub New()
        Text = "Backup automatico giornaliero"
        Width = 780
        Height = 560
        StartPosition = FormStartPosition.CenterScreen

        InizializzaControlli()

        scheduler.Interval = 30000 ' 30 secondi
        AddHandler scheduler.Tick, AddressOf OnSchedulerTick
        scheduler.Start()
    End Sub

    Private Sub InizializzaControlli()
        Dim lblSource As New Label() With {.Text = "Cartella origine:", .Left = 20, .Top = 20, .AutoSize = True}
        txtSource.Left = 160
        txtSource.Top = 16
        txtSource.Width = 580

        Dim lblDestination As New Label() With {.Text = "Cartella destinazione:", .Left = 20, .Top = 60, .AutoSize = True}
        txtDestination.Left = 160
        txtDestination.Top = 56
        txtDestination.Width = 580

        Dim lblTime As New Label() With {.Text = "Orario backup (HH:mm):", .Left = 20, .Top = 100, .AutoSize = True}
        txtTime.Left = 160
        txtTime.Top = 96
        txtTime.Width = 120
        txtTime.Text = "23:30"

        btnStart.Text = "Avvia pianificazione"
        btnStart.Left = 300
        btnStart.Top = 94
        btnStart.Width = 170
        AddHandler btnStart.Click, AddressOf OnStartClick

        btnRunNow.Text = "Esegui backup ora"
        btnRunNow.Left = 480
        btnRunNow.Top = 94
        btnRunNow.Width = 150
        btnRunNow.Enabled = False
        AddHandler btnRunNow.Click, AddressOf OnRunNowClick

        lblStatus.Text = "Stato: in attesa configurazione"
        lblStatus.Left = 20
        lblStatus.Top = 145
        lblStatus.Width = 730

        lblLastRun.Text = "Ultimo backup: --"
        lblLastRun.Left = 20
        lblLastRun.Top = 175
        lblLastRun.Width = 730

        lblNextRun.Text = "Prossimo backup: --"
        lblNextRun.Left = 20
        lblNextRun.Top = 205
        lblNextRun.Width = 730

        progress.Left = 20
        progress.Top = 235
        progress.Width = 720
        progress.Height = 20
        progress.Style = ProgressBarStyle.Continuous
        progress.Value = 0

        txtLog.Left = 20
        txtLog.Top = 270
        txtLog.Width = 720
        txtLog.Height = 230
        txtLog.Multiline = True
        txtLog.ScrollBars = ScrollBars.Vertical
        txtLog.ReadOnly = True

        Controls.Add(lblSource)
        Controls.Add(txtSource)
        Controls.Add(lblDestination)
        Controls.Add(txtDestination)
        Controls.Add(lblTime)
        Controls.Add(txtTime)
        Controls.Add(btnStart)
        Controls.Add(btnRunNow)
        Controls.Add(lblStatus)
        Controls.Add(lblLastRun)
        Controls.Add(lblNextRun)
        Controls.Add(progress)
        Controls.Add(txtLog)
    End Sub

    Private Sub OnStartClick(sender As Object, e As EventArgs)
        If Not Directory.Exists(txtSource.Text) Then
            MessageBox.Show("La cartella origine non esiste.", "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If

        If String.IsNullOrWhiteSpace(txtDestination.Text) Then
            MessageBox.Show("Inserisci una cartella destinazione valida.", "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If

        If Not TimeSpan.TryParse(txtTime.Text, backupTime) Then
            MessageBox.Show("Orario non valido. Usa il formato HH:mm (es. 23:30).", "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If

        Directory.CreateDirectory(txtDestination.Text)

        schedulerEnabled = True
        btnRunNow.Enabled = True
        lblStatus.Text = "Stato: pianificazione attiva"
        AggiornaProssimoBackup()
        ScriviLog("Pianificazione avviata correttamente.")
    End Sub

    Private Async Sub OnRunNowClick(sender As Object, e As EventArgs)
        Await EseguiBackupAsync(forzato:=True)
    End Sub

    Private Async Sub OnSchedulerTick(sender As Object, e As EventArgs)
        If Not schedulerEnabled Then
            Return
        End If

        AggiornaProssimoBackup()
        Await EseguiBackupAsync(forzato:=False)
    End Sub

    Private Async Function EseguiBackupAsync(forzato As Boolean) As Task
        If isRunning Then
            Return
        End If

        Dim adesso As DateTime = DateTime.Now

        If Not forzato Then
            If adesso.TimeOfDay < backupTime Then
                Return
            End If

            If lastRunDate = adesso.Date Then
                Return
            End If
        End If

        isRunning = True
        progress.Style = ProgressBarStyle.Marquee
        progress.MarqueeAnimationSpeed = 35
        lblStatus.Text = "Stato: backup in corso..."

        Dim backupFolderName As String = $"backup_{adesso:yyyyMMdd_HHmmss}"
        Dim destinazioneCompleta As String = Path.Combine(txtDestination.Text, backupFolderName)

        Try
            Await Task.Run(Sub() CopiaDirectoryRicorsiva(txtSource.Text, destinazioneCompleta))
            lastRunDate = DateTime.Now.Date
            lblLastRun.Text = $"Ultimo backup: {DateTime.Now:yyyy-MM-dd HH:mm:ss}"
            ScriviLog($"Backup completato: {destinazioneCompleta}")
            lblStatus.Text = "Stato: backup completato"
        Catch ex As Exception
            ScriviLog($"Errore durante il backup: {ex.Message}")
            lblStatus.Text = "Stato: errore durante il backup"
        Finally
            progress.Style = ProgressBarStyle.Continuous
            progress.Value = 0
            isRunning = False
            AggiornaProssimoBackup()
        End Try
    End Function

    Private Sub AggiornaProssimoBackup()
        If Not schedulerEnabled Then
            lblNextRun.Text = "Prossimo backup: --"
            Return
        End If

        Dim prossimo As DateTime = DateTime.Today.Add(backupTime)
        If DateTime.Now.TimeOfDay >= backupTime Then
            prossimo = prossimo.AddDays(1)
        End If

        lblNextRun.Text = $"Prossimo backup: {prossimo:yyyy-MM-dd HH:mm}"
    End Sub

    Private Sub CopiaDirectoryRicorsiva(sourceDir As String, destinationDir As String)
        Directory.CreateDirectory(destinationDir)

        For Each file As String In Directory.GetFiles(sourceDir)
            Dim destinationFile As String = Path.Combine(destinationDir, Path.GetFileName(file))
            File.Copy(file, destinationFile, overwrite:=True)
        Next

        For Each directory As String In Directory.GetDirectories(sourceDir)
            Dim destinationSubDir As String = Path.Combine(destinationDir, Path.GetFileName(directory))
            CopiaDirectoryRicorsiva(directory, destinationSubDir)
        Next
    End Sub

    Private Sub ScriviLog(messaggio As String)
        txtLog.AppendText($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {messaggio}{Environment.NewLine}")
    End Sub
End Class
