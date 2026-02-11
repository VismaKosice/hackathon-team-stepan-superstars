# https://hub.docker.com/_/microsoft-dotnet
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

# Install NativeAOT build prerequisites
RUN apt-get update \
   && apt-get install -y --no-install-recommends \
      clang zlib1g-dev \
   && rm -rf /var/lib/apt/lists/*

WORKDIR /source

# copy csproj and restore as distinct layers
COPY HackatonAPI/*.csproj ./HackatonAPI/
ENV DOTNET_GENERATE_ASPNET_CERTIFICATE=false
RUN dotnet restore ./HackatonAPI/HackatonAPI.csproj -r linux-x64 /p:PublishAot=true

# copy everything else and build app
COPY HackatonAPI/. ./HackatonAPI/
WORKDIR /source/HackatonAPI/
RUN dotnet publish -o /app -r linux-x64 -c Release --no-restore --self-contained

# final stage/image - use runtime-deps for AOT native binary
FROM mcr.microsoft.com/dotnet/runtime-deps:9.0
WORKDIR /app
COPY --from=build /app ./

# Set default port to 8080, can be overridden with PORT environment variable
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["./HackatonAPI"]