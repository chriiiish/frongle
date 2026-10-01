FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Frongle.sln ./
COPY src/ src/
COPY tests/ tests/
RUN dotnet publish src/Frongle.Api -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
# The base image still ships a vulnerable OpenSSL. Remove this step when the base image has 3.0.13-0ubuntu3.16 or later.
USER root
RUN apt-get update \
    && apt-get install --only-upgrade -y --no-install-recommends libssl3t64 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Frongle.Api.dll"]
