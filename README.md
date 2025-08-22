# OpenNotification-API

Try it at: [https://api.opennotification.org](https://api.opennotification.org)

A simple real-time notification service built with ASP.NET Core and WebSockets. It allows clients to subscribe to a specific channel using a GUID and receive notifications sent to that channel.

---

## Getting Started

### Prerequisites

* .NET 8.0 SDK or later

### Running Locally

1. Clone the repository.
2. Navigate to the project directory `OpenNotification-API`.
3. Run the application using the .NET CLI:

```bash
dotnet run
```

The API will be running on [http://localhost:5193](http://localhost:5193).

---

## API Usage

### 1. Establish a WebSocket Connection

Clients must connect to the WebSocket endpoint to subscribe to a notification channel.

**Endpoint:**

```
GET /ws/{guid}
```

**URL Parameter:**

* `guid` (string, required): A unique identifier for the notification channel.

**Example URL:**

```
ws://localhost:5193/ws/123e4567-e89b-12d3-a456-426614174000
```

Once connected, the client will receive notifications sent to this GUID.

### 2. Send a Notification

Send a POST request to this endpoint to push a notification to all clients subscribed to a specific GUID.

**Endpoint:**

```
POST /notification
```

**Body (JSON):**

```json
{
  "guid": "string (required)",
  "title": "string (required)",
  "description": "string (optional)",
  "pictureLink": "string (optional)",
  "icon": "string (optional)",
  "actionLink": "string (optional)",
  "isAlert": "bool (optional, false)"
}
```

**Example Request:**

```http
POST /notification HTTP/1.1
Host: localhost:5193
Content-Type: application/json

{
  "guid": "123e4567-e89b-12d3-a456-426614174000",
  "title": "New Update",
  "description": "Version 2.0 is now available.",
  "pictureLink": "https://example.com/banner.png",
  "icon": "https://example.com/icon.png",
  "actionLink": "https://example.com/update/2.0",
  "isAlert": true
}
```

**Success Response (200 OK):**

```json
{
  "guid": "123e4567-e89b-12d3-a456-426614174000",
  "title": "New Update",
  "description": "Version 2.0 is now available.",
  "pictureLink": "https://example.com/banner.png",
  "icon": "https://example.com/icon.png",
  "actionLink": "https://example.com/update/2.0",
  "isAlert": true
}
```


