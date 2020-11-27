//-----------------------------------------------------------------------
// <copyright file="LoadTest.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Abune.Server.Cli.Commands
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Abune.Shared.Command;
    using Abune.Shared.Message;
    using Abune.Shared.Protocol;
    using Abune.Shared.Util;
    using System.Globalization;
    using Abune.Shared.DataType;

    /// <summary>Load test command implementation.</summary>
    public class LoadTest : BaseCliCommand
    {
        private class MessageEntry
        {
            public DateTime RequestTimeStamp;
            public DateTime ResponseTimeStamp;
            public ObjectCommandEnvelope CommandEnvelope;
            public int ResponseCount;
        }

        private class ClientStatistic
        {
            public ulong ClientId { get; set; }
            public int MessagesReceived { get; set; }
            public int MessagesSent { get; set; }
            public int UnackedMessages { get; set; }
            public int ResentMessages { get; set; }

            public int DeadLetteredMessages { get; set; }
            public Dictionary<string, MessageStatistic> MessageStatistic = new Dictionary<string, MessageStatistic>();
        }

        private class MessageStatistic
        {
            public int MessagesTotal { get; set; }

            public int MessagesAnswered { get; set; }

            public TimeSpan Duration { get; set; }
        }

        /// <summary>Gets the client count.</summary>
        /// <value>The client count.</value>
        public int ClientCount { get; private set; }

        /// <summary>Gets the message count.</summary>
        /// <value>The message count.</value>
        public int MessageCount { get; private set; }
        
        private const int OPERATION_COUNT = 3;

        private static CountdownEvent _testCountdownEventStart = null;
        private static CountdownEvent _testCountdownEventCreated = null;
        private static CountdownEvent _testCountdownEventUpdate = null;
        private static CountdownEvent _testCountdownEventDestroy = null;

        /// <summary>Initializes a new instance of the <a onclick="return false;" href="LoadTest" originaltag="see">LoadTest</a> class.</summary>
        /// <param name="log">The log.</param>
        /// <param name="parameters">The parameters.</param>
        public LoadTest(TextWriter log, string [] parameters) : base(log, parameters)
        {
            ClientCount = int.Parse(parameters[3], CultureInfo.InvariantCulture);
            MessageCount = int.Parse(parameters[4], CultureInfo.InvariantCulture);
        }

        /// <summary>Runs this instance.</summary>
        public void Run()
        {
            
            Log.WriteLine("Starting clients");
            _testCountdownEventStart = new CountdownEvent(ClientCount + 1);
            _testCountdownEventCreated = new CountdownEvent(ClientCount + 1);
            _testCountdownEventUpdate = new CountdownEvent(ClientCount + 1);
            _testCountdownEventDestroy = new CountdownEvent(ClientCount + 1);

            var tasks = new List<Task<ClientStatistic>>();
            for (uint clientId = 1; clientId < ClientCount + 1; clientId++)
            {
                var task = RunStatisticsClient(clientId, MessageControlFlags.QOS0);
                task.Start();
                tasks.Add(task);
            }

            Task stateReporterTask = new Task(StateReporter);
            stateReporterTask.Start();

            var results = new List<ClientStatistic>();
            foreach (var task in tasks)
            {
                results.Add(task.GetAwaiter().GetResult());
            }

            OutputStatistics(results);            
        }


        private void StateReporter()
        {
            TimeSpan duration = TimeSpan.Zero;
            DateTime startTimeStamp = DateTime.Now;

            Log.Write($"{ClientCount} Clients connecting....");
            _testCountdownEventStart.Signal();
            _testCountdownEventStart.Wait();
            Log.WriteLine($"connected in {DateTime.Now - startTimeStamp}");

            startTimeStamp = DateTime.Now;
            Log.Write($"{ClientCount} x {MessageCount} CREATE operations....");
            _testCountdownEventCreated.Signal();
            _testCountdownEventCreated.Wait();
            Log.WriteLine($"finished in {DateTime.Now - startTimeStamp}");

            startTimeStamp = DateTime.Now;
            Log.Write($"{ClientCount} x {MessageCount} UPDATE operations....");
            _testCountdownEventUpdate.Signal();
            _testCountdownEventUpdate.Wait();
            Log.WriteLine($"finished in {DateTime.Now - startTimeStamp}");

            startTimeStamp = DateTime.Now;
            Log.Write($"{ClientCount} x {MessageCount} DELETE operations....");
            _testCountdownEventDestroy.Signal();
            _testCountdownEventDestroy.Wait();
            Log.WriteLine($"finished in {DateTime.Now - startTimeStamp}");
        }

        private void OutputStatistics(IEnumerable<ClientStatistic> statistics)
        {
            var statisticsNormalized = new Dictionary<string, MessageStatistic>();
            foreach(var statistic in statistics)
            {
                foreach(var messageStatistic in statistic.MessageStatistic)
                {
                    if (!statisticsNormalized.ContainsKey(messageStatistic.Key))
                    {
                        statisticsNormalized.Add(messageStatistic.Key, new MessageStatistic());
                    }
                    statisticsNormalized[messageStatistic.Key].Duration += messageStatistic.Value.Duration;
                    statisticsNormalized[messageStatistic.Key].MessagesTotal += messageStatistic.Value.MessagesTotal;
                    statisticsNormalized[messageStatistic.Key].MessagesAnswered += messageStatistic.Value.MessagesAnswered;
                }
            }

            int messagesTotal = 0;
            int messagesAnsweredTotal = 0;
            TimeSpan maxDuration = TimeSpan.MinValue;

            foreach (var entry in statisticsNormalized)
            {
                TimeSpan avgLatency = entry.Value.MessagesAnswered > 0 ? TimeSpan.FromTicks(entry.Value.Duration.Ticks / entry.Value.MessagesAnswered) : TimeSpan.MaxValue;
                float lossRate = entry.Value.MessagesAnswered > 0 ? 100.0f - ((float)entry.Value.MessagesAnswered / entry.Value.MessagesTotal * 100.0f) : 100.0f;
                double msgpersec = entry.Value.MessagesTotal / entry.Value.Duration.TotalSeconds;
                Log.WriteLine("'{0}': req-msg {1}, resp-msg {2}, avg-latency {3}, loss-rate {4:0.00} %, avg msg/sec {5:0.00}", entry.Key, entry.Value.MessagesTotal, entry.Value.MessagesAnswered, avgLatency, lossRate, msgpersec * ClientCount);
                messagesTotal += entry.Value.MessagesTotal;
                messagesAnsweredTotal += entry.Value.MessagesAnswered;
                maxDuration = TimeSpan.FromTicks(Math.Max(maxDuration.Ticks, entry.Value.Duration.Ticks / OPERATION_COUNT));
            }

            foreach (var clientStatistic in statistics)
            {
                Log.WriteLine("Client [{0}]: received: {1}, sent: {2}, unacked: {3}, resent: {4}, deadlettered: {5}", clientStatistic.ClientId, clientStatistic.MessagesReceived, clientStatistic.MessagesSent, clientStatistic.UnackedMessages, clientStatistic.ResentMessages, clientStatistic.DeadLetteredMessages);
            }

            double msgPerSecTotal = messagesTotal / maxDuration.TotalSeconds;
            float lossRateTotal = messagesAnsweredTotal > 0 ? 100.0f - ((float)messagesAnsweredTotal / messagesTotal * 100.0f) : 100.0f;
            Log.WriteLine("Total: duration: {0} req-msg: {1}, resp-msg: {2}, loss-rate {3:0.00} %", maxDuration, messagesTotal, messagesAnsweredTotal, lossRateTotal);
        }

        private Task<ClientStatistic> RunStatisticsClient(object state, MessageControlFlags qos)
        {
            var task = new Task<ClientStatistic>(() =>
            {
                uint clientId = (uint)state;

                CliClient client = new CliClient();
                AVector3 location = new AVector3
                {
                    X = clientId * Locator.AREASIZE,
                    Y = clientId * Locator.AREASIZE,
                    Z = clientId * Locator.AREASIZE,
                };

                client.Connect(Host, Port, 0, TokenSigningKey, clientId, location);
                AutoResetEvent eventFinished = new AutoResetEvent(false);
                var statistics = new ClientStatistic();
                statistics.ClientId = clientId;
                client.ReliableMessaging.OnDeadLetter = (m) => statistics.DeadLetteredMessages++;
                client.OnConnected = () =>
                {
                    ulong areaId = Locator.GetAreaIdFromWorldPosition(location);

                    _testCountdownEventStart.Signal();
                    _testCountdownEventStart.Wait();

                    statistics.MessageStatistic.Add("CREATE", RunStatistics(client, qos, "Create objects", MessageCount, _ => new ObjectCreateCommand(0, _, 0, 0, 0, location, AQuaternion.Zero)));
                    _testCountdownEventCreated.Signal();
                    _testCountdownEventCreated.Wait();

                    statistics.MessageStatistic.Add("UPDATE", RunStatistics(client, qos, "Update positions", MessageCount, _ => new ObjectUpdatePositionCommand(location, AQuaternion.Zero, AVector3.Zero, AVector3.Zero, 0, 0)));
                    _testCountdownEventUpdate.Signal();
                    _testCountdownEventUpdate.Wait();

                    //statistics.MessageStatistic.Add("DELETE", RunStatistics(client, qos, "Destroy objects", MessageCount, _ => new ObjectDestroyCommand(0, 0, locationX, locationY, locationZ)));
                    _testCountdownEventDestroy.Signal();
                    _testCountdownEventDestroy.Wait();

                    statistics.UnackedMessages = client.ReliableMessaging.MessageUnackedCount;
                    statistics.MessagesSent = client.ReliableMessaging.MessageSentCount;
                    statistics.MessagesReceived = client.ReliableMessaging.MessageReceivedCount;
                    statistics.ResentMessages = client.ReliableMessaging.MessageResentCount;

                    eventFinished.Set();
                };
                eventFinished.WaitOne();
                return statistics;
            });
            return task;
        }

        private MessageStatistic RunStatistics(CliClient client, MessageControlFlags flags, string domain, int messageCount, Func<ulong, BaseCommand> createCommand)
        {
            ConcurrentDictionary<ulong, MessageEntry> messages = new ConcurrentDictionary<ulong, MessageEntry>();
            client.OnCommand = (cmd) => ProcessCommandMessage(client, domain, messages, cmd);
            DateTime startTimeStamp = DateTime.Now;
            ulong objectId = (ulong)(client.ClientId * messageCount);
            for (int i = 0; i < messageCount; i++)
            {
                objectId++;
                BaseCommand command = createCommand(objectId);
                ObjectCommandEnvelope commandEnv = new ObjectCommandEnvelope(client.ClientId, command, objectId);
                messages.TryAdd(objectId, new MessageEntry { RequestTimeStamp = DateTime.Now, ResponseTimeStamp = DateTime.MinValue, CommandEnvelope = commandEnv });
                client.ReliableMessaging.SendCommandEnvelope(commandEnv, flags);
                TimeSpan waitTime = client.ReliableMessaging.SynchronizeMessages();
                if (waitTime > TimeSpan.Zero)
                {
                    Thread.Sleep((int)waitTime.TotalMilliseconds);
                }
            }
            for (; ; )
            {
                if (DateTime.Now - client.LastMessageReceived > TimeSpan.FromSeconds(5))
                {
                    break;
                }
                Thread.Sleep(200);
                TimeSpan waitTime = client.ReliableMessaging.SynchronizeMessages();
                if (waitTime > TimeSpan.Zero)
                {
                    Thread.Sleep((int)waitTime.TotalMilliseconds);
                }
            }
            TimeSpan sumLatency = TimeSpan.Zero;
            int countSuccess = 0;
            foreach (var value in messages.Values)
            {
                if (value.ResponseTimeStamp == DateTime.MinValue)
                {
                    break;
                }
                countSuccess++;
                sumLatency += value.ResponseTimeStamp - value.RequestTimeStamp;
            }
            MessageStatistic messageStatistic = new MessageStatistic();
            messageStatistic.Duration = client.LastMessageReceived - startTimeStamp;
            messageStatistic.MessagesTotal = messageCount;
            messageStatistic.MessagesAnswered = countSuccess;
            return messageStatistic;
        }

        private void ProcessCommandMessage(CliClient client, string domain, ConcurrentDictionary<ulong, MessageEntry> messages, ObjectCommandEnvelope commandEnvelope)
        {
            BaseCommand responseCommand = null;
            switch ((CommandType)commandEnvelope.Command.Type)
            {
                case CommandType.ObjectCreate:
                    responseCommand = new ObjectCreateCommand(commandEnvelope.Command);
                    break;
                case CommandType.ObjectUpdatePosition:
                    responseCommand = new ObjectUpdatePositionCommand(commandEnvelope.Command);
                    break;
                case CommandType.ObjectDestroy:
                    responseCommand = new ObjectDestroyCommand(commandEnvelope.Command);
                    break;
            }
            if (!messages.ContainsKey(commandEnvelope.ToObjectId))
            {
                Log.WriteLine($"[{client.ClientId} ERROR: {domain} mMessage {commandEnvelope.ToObjectId} is missing");
                return;
            }
            messages[commandEnvelope.ToObjectId].ResponseTimeStamp = DateTime.Now;
            messages[commandEnvelope.ToObjectId].ResponseCount++;
            byte[] responseData = commandEnvelope.Serialize();
            byte[] requestData = messages[commandEnvelope.ToObjectId].CommandEnvelope.Serialize();
            if (!ComparyBinary(responseData, requestData))
                Log.WriteLine($"{client.ClientId} ERROR: {domain} message {commandEnvelope.ToObjectId} invalid response");
        }

        private static bool ComparyBinary(byte[] left, byte[] right)
        {
            if (left.Length != right.Length)
                return false;
            for (int i = 0; i < left.Length; i++)
                if (right[i] != left[i])
                    return false;
            return true;
        }
    }
}
