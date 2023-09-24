#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM registry.kube.io/mcr.microsoft.com/dotnet/aspnet:6.0 AS base
RUN apt-get -y update && apt-get install -y curl
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM registry.kube.io/mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["core6.csproj", "."]
RUN dotnet restore "./core6.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "core6.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "core6.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "core6.dll"]