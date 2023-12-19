#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

RUN groupadd -g 1000 smart && \
    useradd -r -u 1000 -g smart smart
RUN chown -R smart:smart /app

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /app
COPY ["HandleMultipleFilesWebApi.csproj", "./"]
RUN dotnet restore "HandleMultipleFilesWebApi.csproj"
COPY . .
RUN dotnet build "HandleMultipleFilesWebApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "HandleMultipleFilesWebApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

USER smart

ENTRYPOINT ["dotnet", "HandleMultipleFilesWebApi.dll"]


