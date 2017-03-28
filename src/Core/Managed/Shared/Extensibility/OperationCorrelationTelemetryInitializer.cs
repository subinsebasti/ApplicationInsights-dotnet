﻿namespace Microsoft.ApplicationInsights.Extensibility
{
#if !NET40
    using System;
    using System.Diagnostics;
    using System.Linq;
#endif
    using Implementation;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.Channel;

#if NET40 || NET45
    /// <summary>
    /// Telemetry initializer that populates OperationContext for the telemetry item based on context stored in CallContext.
    /// </summary>
#else
    /// <summary>
    /// Telemetry initializer that populates OperationContext for the telemetry item based on context stored in an AsyncLocal variable.
    /// </summary>
#endif
    public class OperationCorrelationTelemetryInitializer : ITelemetryInitializer
    {
        /// <summary>
        /// Initializes/Adds operation id to the existing telemetry item.
        /// </summary>
        /// <param name="telemetryItem">Target telemetry item to add operation id.</param>
        public void Initialize(ITelemetry telemetryItem)
        {
            var itemContext = telemetryItem.Context.Operation;

            bool isActivityEnabled = false;
#if !NET40
            isActivityEnabled = ActivityProxy.TryRun(() =>
            {
                if (Activity.Current == null)
                {
                    return false;
                }

                var currentActivity = Activity.Current;

                if (string.IsNullOrEmpty(itemContext.Id))
                {
                    itemContext.Id = currentActivity.RootId;

                    if (string.IsNullOrEmpty(itemContext.ParentId))
                    {
                        itemContext.ParentId = currentActivity.ParentId;
                    }

                    if (telemetryItem is OperationTelemetry)
                    {
                        ((OperationTelemetry)telemetryItem).Id = currentActivity.Id;
                    }
                }

                foreach (var baggage in currentActivity.Baggage)
                {
                    if (!telemetryItem.Context.Properties.ContainsKey(baggage.Key))
                    {
                        telemetryItem.Context.Properties.Add(baggage);
                    }
                }

                string operationName = currentActivity.Tags.FirstOrDefault(tag => tag.Key == "OperationName").Value;

                if (string.IsNullOrEmpty(itemContext.Name) && !string.IsNullOrEmpty(operationName))
                {
                    itemContext.Name = operationName;
                }

                return true;
            });
#endif

            if (!isActivityEnabled)
            {
                if (string.IsNullOrEmpty(itemContext.ParentId) || string.IsNullOrEmpty(itemContext.Id) || string.IsNullOrEmpty(itemContext.Name))
                {
                    var parentContext = CallContextHelpers.GetCurrentOperationContext();
                    if (parentContext != null)
                    {
                        if (string.IsNullOrEmpty(itemContext.ParentId)
                            && !string.IsNullOrEmpty(parentContext.ParentOperationId))
                        {
                            itemContext.ParentId = parentContext.ParentOperationId;
                        }

                        if (string.IsNullOrEmpty(itemContext.Id)
                            && !string.IsNullOrEmpty(parentContext.RootOperationId))
                        {
                            itemContext.Id = parentContext.RootOperationId;
                        }

                        if (string.IsNullOrEmpty(itemContext.Name)
                            && !string.IsNullOrEmpty(parentContext.RootOperationName))
                        {
                            itemContext.Name = parentContext.RootOperationName;
                        }

                        if (parentContext.CorrelationContext != null)
                        {
                            foreach (var item in parentContext.CorrelationContext)
                            {
                                if (!telemetryItem.Context.Properties.ContainsKey(item.Key))
                                {
                                    telemetryItem.Context.Properties.Add(item);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
