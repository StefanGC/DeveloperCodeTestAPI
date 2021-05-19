using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace QueueApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Developer Code Test API with Azure Queue Storage\n");

            // Retrieve the connection string for use with the application.
            string connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");
            string queueName = "user-registrations";

            // Instantiate a QueueClient which will be used to create and manipulate the queue
            QueueClient queueClient = new QueueClient(connectionString, queueName);

            // Create the queue
            await queueClient.CreateAsync();

            // The console shows that we are waiting for new messages
            Console.WriteLine("Receiving messages from the queue...");

            while (true)
            {
                // Get messages from the queue
                QueueMessage[] messages = await queueClient.ReceiveMessagesAsync(maxMessages: 1);

                // Maximum number of attempts to send the email
                int numberOfAttempsAvailable = 5;

                // Get the queue length
                QueueProperties properties = queueClient.GetProperties();
                int cachedMessagesCount = properties.ApproximateMessagesCount;
                // Console.WriteLine($"Number of messages in queue: {cachedMessagesCount}");

                if (cachedMessagesCount < 1000)
                {
                    // Process and delete messages from the queue
                    foreach (QueueMessage message in messages)
                    {
                        // "Process" the message
                        Console.WriteLine($"Message: {message.MessageText}");
                        var array = message.MessageText.Split('"');
                        // Console.WriteLine($"Email is: {array[3]}, firstname is: {array[7]} and lastname is: {array[11]}");

                        // Create the body of the email
                        var jsonText = File.ReadAllText("mailData.json");
                        var body = JsonConvert.DeserializeObject<IList<Body>>(jsonText)[0].body;

                        // Create the email to send
                        MailAddress to = new MailAddress(array[3]);
                        MailAddress from = new MailAddress("info@consulence.com");
                        MailMessage mailMessage = new MailMessage(from, to);
                        mailMessage.Subject = "Welcome as a new customer at Consulence!";
                        mailMessage.SubjectEncoding = System.Text.Encoding.UTF8;
                        mailMessage.Body = $"Hi {array[7]} {array[11]}! <br> {body}";
                        mailMessage.BodyEncoding = System.Text.Encoding.UTF8;
                        // Console.WriteLine($"The body of the emil is: {mailMessage.Body}");

                        SmtpClient client = new SmtpClient(); //new SmtpClient(host);

                        // As long as attempts are available
                        while (numberOfAttempsAvailable > 0)
                        {
                            try
                            {
                                client.Send(mailMessage);
                                mailMessage.Dispose();
                            }
                            catch (Exception ex)
                            {
                                // Console.Error.WriteLine(ex.ToString());
                                numberOfAttempsAvailable--;
                                // Console.WriteLine($"Number of attemps available is {numberOfAttempsAvailable}");
                            }
                        }

                        // Let the service know we're finished with the message and it can be safely deleted.
                        await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt);
                    }
                }
            }

        }
    }
}