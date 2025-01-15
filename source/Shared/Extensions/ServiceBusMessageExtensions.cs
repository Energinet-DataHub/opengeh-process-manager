// Copyright 2020 Energinet DataHub A/S
//
// Licensed under the Apache License, Version 2.0 (the "License2");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Azure.Messaging.ServiceBus;
using Google.Protobuf;

namespace Energinet.DataHub.ProcessManager.Shared.Extensions;

public enum ServiceBusMessageBodyFormat
{
    Binary = 1,
    Json = 2,
}

public static class ServiceBusMessageExtensions
{
    private const string MajorVersionKey = "MajorVersion";
    private const string BodyFormatKey = "BodyFormat";

    public static string GetMajorVersion(this ServiceBusReceivedMessage message)
    {
        return (string?)message.ApplicationProperties.GetValueOrDefault(MajorVersionKey)
               ?? throw new ArgumentNullException(
                   nameof(message.ApplicationProperties),
                   $"{MajorVersionKey} must be present in the ApplicationProperties of the received service bus message (MessageId={message.MessageId}, Subject={message.Subject}).");
    }

    public static ServiceBusMessageBodyFormat GetBodyFormat(this ServiceBusReceivedMessage message)
    {
        var bodyFormatAsString = (string?)message.ApplicationProperties.GetValueOrDefault(BodyFormatKey)
               ?? throw new ArgumentNullException(
                   nameof(message.ApplicationProperties),
                   $"{BodyFormatKey} must be present in the ApplicationProperties of the received service bus message (MessageId={message.MessageId}, Subject={message.Subject}).");

        return Enum.TryParse(bodyFormatAsString, out ServiceBusMessageBodyFormat bodyFormat)
            ? bodyFormat
            : throw new ArgumentOutOfRangeException(
                BodyFormatKey,
                bodyFormatAsString,
                $"Invalid body format value in received service bus message application properties (MessageId={message.MessageId}, Subject={message.Subject}).");
    }

    /// <summary>
    /// Wrap the protobuf message in a service bus message, with the given subject and idempotency key.
    /// This also sets the content type, major version and body format.
    /// </summary>
    /// <param name="message">The protobuf message is serialized into the body of the Service Bus message.</param>
    /// <param name="subject">The subject of the service bus message, used for routing to the correct topic subscription.</param>
    /// <param name="idempotencyKey">
    /// The idempotency key is sent as <see cref="ServiceBusMessage.MessageId"/> on the
    /// Service Bus message, and is used to check for idempotency.
    /// </param>
    public static ServiceBusMessage ToServiceBusMessage(this IMessage message, string subject, string idempotencyKey)
    {
        ServiceBusMessage serviceBusMessage = new(JsonFormatter.Default.Format(message))
        {
            Subject = subject,
            MessageId = idempotencyKey,
            ContentType = "application/json",
        };

        serviceBusMessage.ApplicationProperties.Add(MajorVersionKey, message.GetType().Name);
        serviceBusMessage.ApplicationProperties.Add(BodyFormatKey, ServiceBusMessageBodyFormat.Json.ToString());

        return serviceBusMessage;
    }

    public static TMessage ParseMessageBody<TMessage>(this ServiceBusReceivedMessage message)
        where TMessage : IMessage<TMessage>, new()
    {
        var bodyFormat = message.GetBodyFormat();

        var parser = new MessageParser<TMessage>(() => new TMessage());

        var result = bodyFormat switch
        {
            ServiceBusMessageBodyFormat.Binary => parser.ParseFrom(message.Body),
            ServiceBusMessageBodyFormat.Json => parser.ParseJson(message.Body.ToString()),
            _ => throw new ArgumentOutOfRangeException(
                nameof(bodyFormat),
                bodyFormat,
                $"Unhandled body format in received service bus message (MessageId={message.MessageId})."),
        };

        if (result is null)
        {
            var messageBody = message.Body.ToString();
            throw new ArgumentException("Unable to parse received service bus message body.")
            {
                Data =
                {
                    { "TargetType", typeof(TMessage) },
                    { "BodyFormat", bodyFormat.ToString() },
                    {
                        "Body", messageBody.Length < 1000
                            ? messageBody
                            : messageBody.Substring(0, 1000)
                    },
                    { "MessageId", message.MessageId },
                    { "Subject", message.Subject },
                },
            };
        }

        return result;
    }
}
