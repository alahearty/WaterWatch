using Swashbuckle.AspNetCore.SwaggerUI;

namespace WatchWaterConsumption.Extensions
{
    internal static class SwaggerDocumentationExtension
    {
        internal static IApplicationBuilder UseSwaggerConfig(this IApplicationBuilder app)
        {
            app.UseSwagger();

            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "WaterConsumption API Version 1");
                c.SupportedSubmitMethods(new[] {
                    SubmitMethod.Get, SubmitMethod.Post,
                    SubmitMethod.Put, SubmitMethod.Patch,
                    SubmitMethod.Delete
                });
            });
            return app;
        }

    }
}
