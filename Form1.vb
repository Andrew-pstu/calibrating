Imports System.ComponentModel

Public Class Form1
    Public Ba() As Byte
    Public Калибровка_Напряжение(0 To 2, 0 To 7) As Long
    Public Калибровка_Температура(0 To 2, 0 To 7) As Long
    Public Input_Range As Byte
    Public Output_Current As Byte
    Public N_channel As Byte
    Public Control_byte(0) As Byte
    Public Y1 As Integer
    Public Y1_prev As Integer
    Public N As Integer ' число проходов фильтра
    Public a As Double ' коэф фильтра
    Public Iteraciya As Integer
    Public Start_Flag As Boolean
    Public Measured_value_ch1 As Long ' переменная результата измерения канала1
    Public Measured_value_ch0 As Long ' переменная результата измерения канала2
    Public Флаг_готовности_результата_измерения As Boolean
    Public Результат_АЦП_канал0_прямой_ток As Long
    Public Результат_АЦП_канал1_прямой_ток As Long
    Public Результат_АЦП_канал0_обратный_ток As Long
    Public Результат_АЦП_канал1_обратный_ток As Long
    Public Температура As Long
    Public Stop_Flag As Boolean


    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ReDim Ba(0 To 7)
        Флаг_готовности_результата_измерения = False
        AxMSComm1.InputMode = MSCommLib.InputModeConstants.comInputModeBinary
        AxMSComm1.PortOpen = True
        RadioButton1.Checked = True
        Stop_Flag = False
    End Sub

    Private Sub Form1_Closing(sender As Object, e As CancelEventArgs) Handles Me.Closing
        If AxMSComm1.PortOpen = True Then AxMSComm1.PortOpen = False
    End Sub

    Private Sub AxMSComm1_OnComm(sender As Object, e As EventArgs) Handles AxMSComm1.OnComm
        ' принимаем первые 8 байт результата измерения (3 байта-прямой ток и 3 байта -обратный и 2 байта температуры)
        Ba = AxMSComm1.Input
        If Ba.Length < 8 Then Exit Sub
        Результат_АЦП_канал1_прямой_ток = Ba(0) * 256 * 256 + Ba(1) * 256 + Ba(2)
        Результат_АЦП_канал1_обратный_ток = Ba(3) * 256 * 256 + Ba(4) * 256 + Ba(5)
        Measured_value_ch1 = Результат_АЦП_канал1_прямой_ток - Результат_АЦП_канал1_обратный_ток
        Температура = Ba(6) * 256 + Ba(7)
        Флаг_готовности_результата_измерения = True
    End Sub

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click ' кнопка "выполнить 1 измерение"
        ReDim Калибровка_Напряжение(0 To 2, 0 To 7)
        ReDim Калибровка_Температура(0 To 2, 0 To 7)
        Dim Prev_Cur As Integer
        Dim Prev_Range As Integer
        'System.Threading.Thread.Sleep(3)
        Input_Range = 7
        Output_Current = 1
        Stop_Flag = False
        N = CInt(TextBox15.Text)

        Do 'цикл измерений с перебором предела и тока
            If Stop_Flag = True Then Exit Do
            Измерение()
            If (Measured_value_ch1 > 0.9 * 16777216) And (Output_Current = 1) And (Input_Range = 7) Then
                Stop_Flag = True : Button3_Click(sender, e) : MsgBox("R слишком велико!")
                Exit Sub
            End If

            a = 0.92 ' фильтр ослабляем для быстрейшего установления
            Y1 = 0 : Y1_prev = 0 : Iteraciya = 0
            Y1_prev = Measured_value_ch1 ' присваиваем предыдущему результату фильтрации текущее измеренное значение,
            ' чтобы ползти к измеряемуму значению не от нуля, что быстрее
            TextBox12.Text = Output_Current
            TextBox13.Text = Input_Range

            Цикл_измерений_с_фильтрацией()
            Калибровка_Напряжение(Output_Current - 1, Input_Range) = Y1
            Калибровка_Температура(Output_Current - 1, Input_Range) = Температура
            'если измеренное значение меньше 1/2 предела и еще есть куда уменьшать предел, то уменьшить предел
            If (Y1 <= 0.9 / 2 * 16777216) And (Input_Range <> 0) Then
                Input_Range = Input_Range - 1
            End If
            'если диапазон уменьшать уже некуда, а измеренное значение меньше 1/10 предела и вых ток не на пределе, то увеличить ток
            If (Prev_Range = Input_Range) Then
                Input_Range = 7 : Измерение() 'меняем предел на макс, чтобы увидеть можно ли увеличивать ток
                If (Measured_value_ch1 <= 0.09 * 16777216) And (Output_Current <> 3) Then
                    Output_Current = Output_Current + 1
                Else
                    Input_Range = Prev_Range ' иначе восстановить предел
                End If
            End If

            If (Prev_Cur = Output_Current) And (Prev_Range = Input_Range) Then
                Exit Do
            End If
            Prev_Range = Input_Range
            Prev_Cur = Output_Current
        Loop
        Button3_Click(sender, e) ' кнопка "СТОП"
        Запись_в_файл()
    End Sub

    Private Sub Цикл_измерений_с_фильтрацией()
        Dim Eps As Double
        Do
            If Stop_Flag = True Then Exit Do

            TextBox16.Text = a
            Iteraciya = Iteraciya + 1 ' счетчик проходов фильтра
            Y1 = Y1_prev * a + Measured_value_ch1 * (1 - a)
            TextBox10.Text = Y1 'вывод отфильтрованного значения
            Eps = Math.Abs((Y1 - Y1_prev) / Y1)
            Application.DoEvents()

            Control_byte(0) = N_channel + Input_Range * 2 + Output_Current * 16
            AxMSComm1.Output = Control_byte ' запуск измерения
            Флаг_готовности_результата_измерения = False
            Do While Флаг_готовности_результата_измерения = False ' ждем ответа от прибора
                Application.DoEvents() ' даем системе обрабатывать события
            Loop
            Y1_prev = Y1
            TextBox11.Text = Iteraciya
            If Iteraciya > N / 2 Then a = 0.99 ' врубаем фильтр на полную, чтобы меньше колбасило результат
            If Iteraciya > N * 2 Then a = 0.999 'если результат колбасит уже слишком долго, то затягиваем фильтр до предела
            TextBox1.Text = Результат_АЦП_канал1_прямой_ток
            TextBox2.Text = Результат_АЦП_канал1_обратный_ток
            TextBox3.Text = Measured_value_ch1
            TextBox14.Text = Format((Температура * 0.007153 - 254.1624), "00.0") 'Format(((Температура - 32768) / 256), "00.0") 'переводим результат АЦП показаний встроенного в ADuC824 термосенсора  в градусы цельсия
            TextBox17.Text = Температура

        Loop Until (Eps < 0.000001 And Iteraciya >= N) Or (Stop_Flag) ' если достигли заданной малости колебаний результата фильтра Eps > 0.0001 And 

    End Sub

    Private Sub Button3_Click(sender As Object, e As EventArgs) Handles Button3.Click ' кнопка "СТОП"
        Application.DoEvents()
        System.Threading.Thread.Sleep(500)
        Application.DoEvents()
        Stop_Flag = True
        Выкл_источник_тока()
    End Sub

    Private Sub Измерение()
        Control_byte(0) = N_channel + Input_Range * 2 + Output_Current * 16
        AxMSComm1.Output = Control_byte ' запуск измерения
        Флаг_готовности_результата_измерения = False
        Do While Флаг_готовности_результата_измерения = False ' ждем ответа от прибора
            Application.DoEvents() ' даем системе обрабатывать события
        Loop
    End Sub

    Private Sub Выкл_источник_тока()
        Output_Current = 0
        Input_Range = CByte(7)
        Control_byte(0) = 1 + Input_Range * 2 + Output_Current * 16
        AxMSComm1.Output = Control_byte
        Application.DoEvents()
    End Sub

    Private Sub Запись_в_файл()
        If IO.File.Exists("test.txt") = False Then ' если Файл не существует
            My.Computer.FileSystem.WriteAllText("test.txt", "", True) ' создаем пустой файл
        End If

        For i = 0 To 2
            For j = 0 To 7
                FileOpen(1, "test.txt", OpenMode.Append)
                Print(1, TextBox4.Text + " " + CStr(i + 1) + " " + CStr(j) + " " + CStr(Калибровка_Напряжение(i, j)) + " " + CStr(Калибровка_Температура(i, j)) + vbNewLine)
                FileClose(1)
            Next j
        Next i
    End Sub


    Private Sub RadioButton1_CheckedChanged(sender As Object, e As EventArgs) Handles RadioButton1.CheckedChanged
        If RadioButton1.Checked = True Then
            N_channel = 1
        Else
            N_channel = 0
        End If
    End Sub

    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click
        My.Computer.FileSystem.WriteAllText("test.txt", "", False) ' создаем пустой файл
    End Sub
End Class
