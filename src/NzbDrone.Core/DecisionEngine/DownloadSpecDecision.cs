using System;
using System.Collections.Generic;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Localization;

namespace NzbDrone.Core.DecisionEngine
{
    public class DownloadSpecDecision
    {
        private static readonly DownloadSpecDecision AcceptDownloadSpecDecision = new () { Accepted = true };

        private DownloadSpecDecision()
        {
        }

        public bool Accepted { get; private set; }
        public DownloadRejectionReason Reason { get; set; }
        public string Message => GetMessage(null);
        public string MessageKey { get; private set; }
        public string MessageTemplate { get; private set; }
        public object[] MessageArgs { get; private set; } = Array.Empty<object>();

        public static DownloadSpecDecision Accept()
        {
            return AcceptDownloadSpecDecision;
        }

        public static DownloadSpecDecision Reject(DownloadRejectionReason reason, string message, params object[] args)
        {
            return RejectWithKey(reason, $"DownloadRejection{reason}", message, args);
        }

        public static DownloadSpecDecision RejectWithKey(DownloadRejectionReason reason, string messageKey, string message, params object[] args)
        {
            return new DownloadSpecDecision
            {
                Accepted = false,
                Reason = reason,
                MessageKey = messageKey,
                MessageTemplate = message,
                MessageArgs = args ?? Array.Empty<object>()
            };
        }

        public static DownloadSpecDecision Reject(DownloadRejectionReason reason, string message)
        {
            return RejectWithKey(reason, $"DownloadRejection{reason}", message, Array.Empty<object>());
        }

        public string GetMessage(ILocalizationService localizationService)
        {
            var tokens = new Dictionary<string, object>();

            for (var i = 0; i < MessageArgs.Length; i++)
            {
                tokens[$"arg{i}"] = MessageArgs[i];
            }

            var localized = localizationService?.GetLocalizedString(MessageKey, tokens);

            if (localized.IsNotNullOrWhiteSpace() && localized != MessageKey)
            {
                return localized;
            }

            return MessageArgs.Length == 0 ? MessageTemplate : string.Format(MessageTemplate, MessageArgs);
        }
    }
}
