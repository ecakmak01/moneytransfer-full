# Money Transfer Microservices – README

## 📌 Proje Hakkında  
Bu proje iş görüşmesi için hazırlanmış olup **mikroservis mimarisi**, **gözlemlenebilirlik**, **idempotency**, **gateway/BFF pattern**, **Docker Compose orkestrasyonu** ve **Helm chart** kullanımını içeren tam uçtan uca bir demo sunar.

Proje;  
- **account-service**  
- **moneytransfer-service**  
- **gateway-bff**  
- **ui (React/HTML)**  
- **PostgreSQL**  
bileşenlerinden oluşur.

---

## 🏗️ Kullanılan Teknolojiler

### Backend
- **.NET 8 Web API**
- **Entity Framework Core 8**
- **Npgsql (PostgreSQL) Provider**
- **JWT Authentication**
- **Serilog (JSON logging + correlation ID enrichment)**
- **Idempotency Key Pattern**

### Frontend
- **Nginx üzerinden servis edilen basit HTML/JS UI**

### Deploy / Orkestrasyon
- **Docker Compose**
- **Helm Chart (Kubernetes ortamı için)**

---

## 🧩 Mimari Tercihler

### 1. Mikroservis Mimarisi  
Her servis tek bir bounded context’e odaklanır:  
- **Account Service:** Hesap yönetimi, bakiye kontrolü, idempotency  
- **Money Transfer Service:** Transfer işlemi ve transactional akış  
- **Gateway-BFF:** UI isteklerini backend’e yönlendirme, CORS ve güvenlik soyutlama

### 2. Gözlemlenebilirlik (Observability)
- **Correlation ID Middleware** ile her isteğe `X-Correlation-ID` eklenir.  
- Serilog log’ları JSON formatında tutar → Log management sistemlerine rahat gönderilebilir.

### 3. Dayanıklılık (Resilience)
- **Idempotency-Key** ile aynı transfer istekleri tekrar çalıştırılmaz.  
- DB transaction kullanılarak tutarlılık sağlanır.

---

## 🛠️ Kurulum Adımları

### 1. Gereksinimler
- Docker Desktop  
- .NET 8 SDK (opsiyonel – geliştirme için)

### 2. Projeyi Çalıştırma
Aşağıdaki tek komut ile tüm sistem ayağa kalkar:

```sh
docker compose up --build
```

UI erişimi:  
👉 **http://localhost:3000**

Gateway erişimi:  
👉 **http://localhost:5000**

Account-service Swagger:  
👉 http://localhost:5001/swagger

Money-transfer-service Swagger:  
👉 http://localhost:5002/swagger

---

## 🗄️ Veritabanı Migration’ları
Her servis kendi migrationlarını içerir.  
Docker Compose ayağa kalkarken otomatik olarak DB oluşturulur.

---

## 🧪 Basit Test Senaryoları

### 1. Token Alma
```
POST http://localhost:5000/auth/token
{
  "username": "emre"
}
```

### 2. Hesap Oluşturma
```
POST http://localhost:5001/api/accounts
Authorization: Bearer <TOKEN>
```

### 3. Transfer İşlemi
```
POST http://localhost:5002/api/transfer
Idempotency-Key: test-123
Authorization: Bearer <TOKEN>
```

---

## 📦 Kubernetes (Helm Chart)
`/helm/moneytransfer` altında basit bir chart bulunur.  
Kullanmak için:

```sh
helm install moneytransfer ./helm/moneytransfer
```

---

## 📁 Repo İçeriği

```
/account-service
/moneytransfer-service
/gateway-bff
/ui
/helm
docker-compose.yml
README.md
initial_migration.sql
test-scenarios.txt
```

---

## 👍 Sonuç  
Bu repo; mikroservis mimarisi, resiliency pattern’leri, gözlemlenebilirlik, BFF pattern ve container orkestrasyonu konularında modern bir demo sunar.  
