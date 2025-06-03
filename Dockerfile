########################################
# 1) BUILD STAGE
########################################

# Базовий образ з повним SDK .NET 8.0 (Linux)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build 
WORKDIR /src

# 1.1) Копіюємо тільки .csproj, щоб скористатися кешем Docker для dotnet restore
#      Якщо у вас інша назва (не "SteamStoreBot_V8.csproj"), замініть її відповідно.
COPY ["SteamStoreBot_V8.csproj", "./"]

# 1.2) Виконуємо dotnet restore, щоби завантажити всі PackageReference
RUN dotnet restore "SteamStoreBot_V8.csproj"

# 1.3) Копіюємо повністю весь вихідний код у контейнер
COPY . .

# 1.4) Виконуємо dotnet publish (Release), результати виводяться в папку /app/publish
RUN dotnet publish "SteamStoreBot_V8.csproj" -c Release -o /app/publish


########################################
# 2) RUNTIME STAGE
########################################

# Базовий образ тільки з .NET Runtime 8.0 (Linux) — вже без SDK
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app

# 2.1) Копіюємо результат публікації з попереднього етапу
COPY --from=build /app/publish ./

# 2.2) Встановлюємо точку входу:
#      Тут ми запускаємо саме згенерований .dll файл.
ENTRYPOINT ["dotnet", "SteamStoreBot_V8.dll"]
