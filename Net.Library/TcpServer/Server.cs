using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SomeProject.Library.Server
{

    public class Server
    {

        TcpListener serverListener;
        static int fileNum = 0, count = 0;

        public Server()
        {
            serverListener = new TcpListener(IPAddress.Loopback, 8080);
        }

        /// <summary>
        /// Выключение listener
        /// </summary>
        /// <returns>true/false</returns>
        public bool TurnOffListener()
        {
            try
            {
                if (serverListener != null)
                    serverListener.Stop();
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine("Cannot turn off listener: " + e.Message);
                return false;
            }
        }

        /// <summary>
        /// Включение listener
        /// </summary>
        /// <returns></returns>
        public async Task TurnOnListener()
        {
            try
            {
                if (serverListener != null)
                    serverListener.Start();
                Console.WriteLine("Server online");
                ThreadPool.SetMaxThreads(10, 10); 
                ThreadPool.SetMinThreads(2, 2);
                while (true)
                    ThreadPool.QueueUserWorkItem(new WaitCallback(Callback), serverListener.AcceptTcpClient());
            }
            catch (Exception e)
            {
                Console.WriteLine("Cannot turn on listener: " + e.Message);
            }
        }

        /// <summary>
        /// Обратока срабатывания tcp-клиента
        /// </summary>
        /// <param name="client">Клиент</param>
        static void Callback(object client)
        {
            Console.WriteLine("New connection " + Interlocked.Increment(ref count));
            OperationResult result = ReceiveHeaderFromClient((TcpClient)client).Result;
            if (result.Result == Result.Fail)
                Console.WriteLine("Error: " + result.Message);
            else
                Console.WriteLine("Message from client: " + result.Message);
            Interlocked.Decrement(ref count);
        }

        /// <summary>
        /// Обработка файла/сообщения
        /// </summary>
        /// <param name="client">Клиент</param>
        /// <returns></returns>
        public async static Task<OperationResult> ReceiveHeaderFromClient(TcpClient client)
        {
            try
            {
                StringBuilder recievedMessage = new StringBuilder();
                byte[] data = new byte[256];
                string msg = "";
                using (NetworkStream stream = client.GetStream())
                {
                    int bytes = await stream.ReadAsync(data, 0, data.Length);
                    recievedMessage.Append(Encoding.UTF8.GetString(data, 0, bytes));

                    if ((new Regex(@"@file: (.*)@")).IsMatch(recievedMessage.ToString()))
                    {
                        int delimetr = recievedMessage.ToString().IndexOf('@', 1);
                        Console.WriteLine(recievedMessage.ToString().Substring(0, delimetr));
                        SendMessageToClient(stream, ReceiveFileFromClient(stream, recievedMessage.ToString().Substring(7, delimetr - 7).Split('.')[0],
                            recievedMessage.ToString().Substring(delimetr + 1)).Message);
                    }
                    else
                    {
                        if (stream.DataAvailable)
                            msg = ReceiveMessageFromClient(stream, recievedMessage);
                        else
                            msg = recievedMessage.ToString();
                        SendMessageToClient(stream, msg);
                    }
                }
                client.Close();
                return new OperationResult(Result.OK, msg);
            }
            catch (Exception e)
            {
                return new OperationResult(Result.Fail, e.Message);
            }
        }

        /// <summary>
        /// Получение сообщения от клиента
        /// </summary>
        /// <param name="stream">Входящий поток</param>
        /// <param name="recievedMessage">Проверка на файл или не файл</param>
        /// <returns></returns>
        private static string ReceiveMessageFromClient(NetworkStream stream, StringBuilder recievedMessage)
        {
            try
            {
                byte[] data = new byte[256];
                do
                {
                    int bytes = stream.Read(data, 0, data.Length);
                    recievedMessage.Append(Encoding.UTF8.GetString(data, 0, bytes));
                }
                while (stream.DataAvailable);
                Console.WriteLine("> " + recievedMessage.ToString());
                return recievedMessage.ToString();
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        /// <summary>
        /// Получение файла от клиента
        /// </summary>
        /// <param name="stream">Входящий поток</param>
        /// <param name="ext">Расширение файла</param>
        /// <param name="head">Начало файла</param>
        /// <returns></returns>
        private static OperationResult ReceiveFileFromClient(NetworkStream stream, string ext, string head)
        {
            try
            {
                StringBuilder recievedMessage = new StringBuilder().Append(head);

                byte[] data = new byte[4096];
                if (!Directory.Exists(DateTime.Now.ToString("yyyy-MM-dd")))
                    Directory.CreateDirectory(DateTime.Now.ToString("yyyy-MM-dd"));
                int fn = Interlocked.Increment(ref fileNum);
                using (FileStream fstream = new FileStream(DateTime.Now.ToString("yyyy-MM-dd") + "\\" + fn.ToString() + "." + ext, FileMode.Create))
                    do
                    {
                        int bytes = stream.Read(data, 0, data.Length);
                        fstream.Write(data, 0, bytes);
                    }
                    while (stream.DataAvailable);

                return new OperationResult(Result.OK, DateTime.Now.ToString("yyyy-MM-dd") + "\\" + fn.ToString() + "." + ext);
            }
            catch (Exception e)
            {
                return new OperationResult(Result.Fail, e.Message);
            }
        }

        /// <summary>
        /// Отправка сообщения клиенту
        /// </summary>
        /// <param name="stream">Поток</param>
        /// <param name="message">Сообщение</param>
        /// <returns></returns>
        private static OperationResult SendMessageToClient(NetworkStream stream, string message)
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(message);
                stream.Write(data, 0, data.Length);
            }
            catch (Exception e)
            {
                return new OperationResult(Result.Fail, e.Message);
            }
            return new OperationResult(Result.OK, "");
        }
    }
}