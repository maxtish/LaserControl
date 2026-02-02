using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace LaserPanelTCP
{
    public partial class MainWindow : Window
    {
        // TCP клиент
        private TcpClient client;

        // Поток данных
        private NetworkStream stream;

        // Состояние лазера
        private bool laserState = false;

        // Поток приёма данных
        private Thread rxThread;

        // Токен для остановки цикла подключения
        private CancellationTokenSource connectCts = new CancellationTokenSource();

        // Флаг предыдущего соединения
        private bool wasConnected = false;

        // === КОНСТРУКТОР ===
        public MainWindow()
        {
            InitializeComponent();

            // Стартовый статус
            StatusLabel.Content = "Подключение...";
            StatusLabel.Foreground = Brushes.Orange;

            // Запускаем цикл подключения
            StartConnectLoop();
        }

        // === Цикл проверки соединения раз в секунду ===
        private async void StartConnectLoop()
        {
            while (!connectCts.IsCancellationRequested)
            {
                // Если клиента нет или не подключен — пробуем подключиться
                if (client == null || !client.Connected)
                {
                    TryConnect();
                }

                // Ждём 1 секунду
                await Task.Delay(1000);
            }
        }

        // === Попытка подключения ===
        private void TryConnect()
        {
            try
            {
                // Закрываем старый клиент
                client?.Close();

                // Создаём нового
                client = new TcpClient();
                client.Connect("127.0.0.1", 12345);

                // Получаем поток
                stream = client.GetStream();

                // Запускаем поток чтения
                rxThread = new Thread(ReadLoop);
                rxThread.IsBackground = true;
                rxThread.Start();

                wasConnected = true;

                // Обновляем UI
                Dispatcher.Invoke(() =>
                {
                    StatusLabel.Content = "Подключено";
                    StatusLabel.Foreground = Brushes.Green;

                    LogBox.AppendText("Подключено!\n");
                    LogBox.ScrollToEnd();
                });
            }
            catch
            {
                // Если не получилось подключиться
                if (wasConnected)
                {
                    wasConnected = false;

                    Dispatcher.Invoke(() =>
                    {
                        StatusLabel.Content = "Ожидание сервера...";
                        StatusLabel.Foreground = Brushes.Orange;
                    });
                }
            }
        }

        // === Приём данных ===
        private void ReadLoop()
        {
            byte[] buf = new byte[256];

            while (true)
            {
                try
                {
                    // Читаем из сокета
                    int len = stream.Read(buf, 0, buf.Length);

                    if (len == 0)
                        throw new Exception();

                    string text = Encoding.UTF8.GetString(buf, 0, len);

                    // Обновляем UI
                    Dispatcher.Invoke(() =>
                    {
                        LogBox.AppendText(text);
                        LogBox.ScrollToEnd();
                    });
                }
                catch
                {
                    // Если соединение оборвалось
                    Dispatcher.Invoke(() =>
                    {
                        StatusLabel.Content = "Соединение потеряно";
                        StatusLabel.Foreground = Brushes.Red;

                        LogBox.AppendText("Соединение потеряно...\n");
                        LogBox.ScrollToEnd();
                    });

                    client?.Close();
                    break;
                }
            }
        }

        // === Кнопка управления лазером ===
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // Проверяем соединение
            if (stream == null || client == null || !client.Connected)
            {
                MessageBox.Show("Нет соединения с Renode");
                return;
            }

            // Меняем состояние
            laserState = !laserState;

            string cmd = laserState ? "ON\n" : "OFF\n";

            try
            {
                byte[] data = Encoding.ASCII.GetBytes(cmd);
                stream.Write(data, 0, data.Length);

                LaserButton.Content = laserState ? "STOP LASER" : "START LASER";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка отправки: " + ex.Message);
            }
        }

        // === Закрытие окна ===
        protected override void OnClosed(EventArgs e)
        {
            connectCts.Cancel();
            stream?.Close();
            client?.Close();
            base.OnClosed(e);
        }
    }
}