FROM mcr.microsoft.com/dotnet/runtime:6.0-alpine AS base
RUN apk update && apk add bash && apk add icu-dev

WORKDIR /app

RUN adduser -u 5678 --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine AS build

ARG BAGET_ENV

WORKDIR /src
COPY ["src/Matching.csproj", "./"]
COPY ./src/nuget.config ./

RUN dotnet restore "Matching.csproj"
COPY ./src .
WORKDIR /src
RUN dotnet build "Matching.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Matching.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

