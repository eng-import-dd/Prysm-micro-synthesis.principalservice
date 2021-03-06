using Microsoft.Owin;
using Synthesis.Logging;
using System;
using System.Threading.Tasks;
using Synthesis.Tracking;

namespace Synthesis.PrincipalService.Owin
{
    public class GlobalExceptionHandlerMiddleware : OwinMiddleware
    {
        private const string ServiceName = "PrincipalService";
        private readonly ILogger _logger;
        private readonly ITrackingService _tracking;

        /// <inheritdoc />
        public GlobalExceptionHandlerMiddleware(OwinMiddleware next, ILoggerFactory loggerFactory, ITrackingService tracking) : base(next)
        {
            _logger = loggerFactory.GetLogger(this);
            _tracking = tracking;
        }

        /// <inheritdoc />
        public override async Task Invoke(IOwinContext context)
        {
            try
            {
                await Next.Invoke(context);
            }
            catch (Exception ex)
            {
                await _tracking.TrackExceptionAsync(ex);

                try
                {
                    _logger.Error($"Unhandled exception detected in {ServiceName}", ex);
                }
                catch (Exception ex2)
                {
                    await _tracking.TrackExceptionAsync(ex2);
                }
            }
        }
    }
}
