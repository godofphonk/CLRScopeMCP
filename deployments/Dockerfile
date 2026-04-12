# Multi-stage Dockerfile for CLRScope MCP Server
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY src/ClrScope.Mcp/ClrScope.Mcp.csproj src/ClrScope.Mcp/
RUN dotnet restore src/ClrScope.Mcp/ClrScope.Mcp.csproj

# Copy everything else and build
COPY src/ClrScope.Mcp/ src/ClrScope.Mcp/
RUN dotnet publish src/ClrScope.Mcp/ClrScope.Mcp.csproj \
    -c Release \
    -o /app/publish \
    --self-contained false \
    /p:PublishSingleFile=false

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install diagnostic tools
RUN apt-get update && apt-get install -y \
    wget \
    && rm -rf /var/lib/apt/lists/*

# Install dotnet-dump
RUN wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh \
    && chmod +x dotnet-install.sh \
    && ./dotnet-install.sh --channel 9.0 --runtime dotnet --install-dir /usr/share/dotnet \
    && rm dotnet-install.sh

# Install dotnet-symbol
RUN /usr/share/dotnet/dotnet tool install -g dotnet-symbol

# Add dotnet tools to PATH
ENV PATH="${PATH}:/root/.dotnet/tools"

# Copy published app
COPY --from=build /app/publish /app

# Create directory for artifacts and database
RUN mkdir -p /root/.clrscope/artifacts \
    && mkdir -p /root/.clrscope/data

# Set environment variables
ENV CLRSCOPE__ArtifactRoot=/root/.clrscope/artifacts
ENV CLRSCOPE__DatabasePath=/root/.clrscope/data/clrscope.db

# Expose MCP server port (if using stdio, this is not needed)
# EXPOSE 8080

# Run the app
ENTRYPOINT ["dotnet", "/app/ClrScope.Mcp.dll"]
