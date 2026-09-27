FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY NuGet.config ./
COPY src/CodeReviewAgent.Api/CodeReviewAgent.Api.csproj src/CodeReviewAgent.Api/
RUN dotnet restore src/CodeReviewAgent.Api/CodeReviewAgent.Api.csproj

COPY src/CodeReviewAgent.Api/ src/CodeReviewAgent.Api/
RUN dotnet publish src/CodeReviewAgent.Api/CodeReviewAgent.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "CodeReviewAgent.Api.dll"]
