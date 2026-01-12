# Use official .NET 8 SDK image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

# Install EPPlus license (non-commercial)
ENV EPPLUS_LICENSE_NONCOMMERCIAL=Achoi

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["BreaklistWeb.csproj", "./"]
RUN dotnet restore "./BreaklistWeb.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "BreaklistWeb.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "BreaklistWeb.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BreaklistWeb.dll"]
