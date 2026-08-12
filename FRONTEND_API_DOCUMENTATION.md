# API Documentation for Frontend Team

## Base URL
```
https://localhost:7046
```

## Authentication
All protected endpoints require a JWT token in the `Authorization` header:
```
Authorization: Bearer <jwt_token>
```

Get JWT token from login endpoint first.

---

## 📹 Video Upload Endpoints

### Option 1: Presigned URL Upload (Recommended for Large Files)

**Flow:** Frontend requests a presigned URL → Frontend uploads directly to S3 → Frontend saves URL to backend

#### 1. Request Presigned Upload URL

**Endpoint:**
```
POST /api/records/video-upload-url
Content-Type: application/json
Authorization: Bearer <jwt_token>
```

**Request Body:**
```json
{
  "fileName": "my-race-video.mp4",
  "contentType": "video/mp4"
}
```

**Response (200 OK):**
```json
{
  "uploadUrl": "https://s3.amazonaws.com/bucket-name/...",
  "objectKey": "records/2024/abc123.mp4",
  "publicUrl": "https://cloudfront.amazonaws.com/records/2024/abc123.mp4",
  "expiresAtUtc": "2024-01-15T10:30:00Z"
}
```

#### 2. Upload File to S3 using Presigned URL

**Method:** `PUT` (not POST)
```javascript
const presignedUrl = response.uploadUrl;
const file = document.getElementById('videoInput').files[0];

const uploadResponse = await fetch(presignedUrl, {
  method: 'PUT',
  headers: {
	'Content-Type': 'video/mp4'
  },
  body: file
});
```

#### 3. Save Video URL to Database

After uploading to S3, create/update a record with the `videoUrl` from step 1:

```
POST /api/records
Content-Type: application/json
Authorization: Bearer <jwt_token>
```

**Request Body:**
```json
{
  "userId": 1,
  "mapId": 2,
  "vehicleId": 3,
  "gameModeId": 1,
  "title": "My Epic Race",
  "videoUrl": "https://cloudfront.amazonaws.com/records/2024/abc123.mp4",
  "thumbnailUrl": "https://...",
  "finishTime": 120.5,
  "description": "Great lap!"
}
```

---

### Option 2: Direct Upload (For Testing in Swagger)

**Endpoint:**
```
POST /api/records/video-upload
Content-Type: multipart/form-data
Authorization: Bearer <jwt_token>
```

**Form Data:**
- `videoFile` (file, required)

**Response (200 OK):**
```json
{
  "objectKey": "records/2024/abc123.mp4",
  "publicUrl": "https://cloudfront.amazonaws.com/records/2024/abc123.mp4",
  "uploadedAtUtc": "2024-01-15T10:15:00Z"
}
```

---

## 🔐 Authentication Endpoints

### Login
```
POST /api/users/login
Content-Type: application/json
```

**Request:**
```json
{
  "username": "player1",
  "password": "password123"
}
```

**Response (200 OK):**
```json
{
  "id": 1,
  "username": "player1",
  "email": "player1@example.com",
  "token": "eyJhbGciOiJIUzI1NiIs..."
}
```

**Error (401):**
```json
{
  "message": "Invalid username or password"
}
```

### Register
```
POST /api/users/register
Content-Type: application/json
```

**Request:**
```json
{
  "username": "newplayer",
  "email": "newplayer@example.com",
  "password": "securePassword123"
}
```

**Response (201 Created):**
```json
{
  "id": 2,
  "username": "newplayer",
  "email": "newplayer@example.com",
  "token": "eyJhbGciOiJIUzI1NiIs..."
}
```

---

## 📊 Records Endpoints

### Get All Records
```
GET /api/records
```

**Response (200 OK):**
```json
[
  {
	"id": 1,
	"title": "My Epic Race",
	"videoUrl": "https://cloudfront.amazonaws.com/records/2024/abc123.mp4",
	"thumbnailUrl": "https://example.com/thumb.jpg",
	"finishTime": 120.5,
	"description": "Great lap!",
	"views": 42,
	"createdAt": "2024-01-15T10:00:00Z",
	"updatedAt": "2024-01-15T10:00:00Z",
	"user": {
	  "id": 1,
	  "username": "player1",
	  "email": "player1@example.com"
	},
	"map": {
	  "id": 2,
	  "name": "Track 1",
	  "slug": "track-1",
	  "imageUrl": "https://example.com/track1.jpg"
	},
	"vehicle": {
	  "id": 3,
	  "name": "Ferrari",
	  "slug": "ferrari",
	  "imageUrl": "https://example.com/ferrari.jpg"
	},
	"gameMode": {
	  "id": 1,
	  "name": "Time Trial",
	  "description": "Race against the clock"
	}
  }
]
```

### Get Single Record
```
GET /api/records/{id}
```

### Create Record (After S3 Upload)
```
POST /api/records
Content-Type: application/json
Authorization: Bearer <jwt_token>
```

**Request:**
```json
{
  "userId": 1,
  "mapId": 2,
  "vehicleId": 3,
  "gameModeId": 1,
  "title": "My Epic Race",
  "videoUrl": "https://cloudfront.amazonaws.com/records/2024/abc123.mp4",
  "thumbnailUrl": "https://example.com/thumb.jpg",
  "finishTime": 120.5,
  "description": "Great lap!"
}
```

### Update Record
```
PUT /api/records/{id}
Content-Type: application/json
Authorization: Bearer <jwt_token>
```

### Delete Record
```
DELETE /api/records/{id}
Authorization: Bearer <jwt_token>
```

---

## 🗺️ Maps Endpoints

### Get All Maps
```
GET /api/maps
```

### Get Single Map
```
GET /api/maps/{id}
```

### Create Map
```
POST /api/maps
Content-Type: application/json
Authorization: Bearer <jwt_token>
```

**Request:**
```json
{
  "name": "Track 1",
  "slug": "track-1",
  "description": "First racing track"
}
```

---

## 🚗 Vehicles Endpoints

### Get All Vehicles
```
GET /api/vehicles
```

### Get Single Vehicle
```
GET /api/vehicles/{id}
```

### Create Vehicle
```
POST /api/vehicles
Content-Type: application/json
Authorization: Bearer <jwt_token>
```

**Request:**
```json
{
  "name": "Ferrari",
  "slug": "ferrari",
  "manufacturer": "Ferrari",
  "description": "Fast Italian car"
}
```

---

## 🎮 Game Modes Endpoints

### Get All Game Modes
```
GET /api/gamemodes
```

### Get Single Game Mode
```
GET /api/gamemodes/{id}
```

### Create Game Mode
```
POST /api/gamemodes
Content-Type: application/json
Authorization: Bearer <jwt_token>
```

**Request:**
```json
{
  "name": "Time Trial",
  "description": "Race against the clock"
}
```

---

## ⚠️ Error Handling

All error responses follow this format:

**400 Bad Request:**
```json
{
  "message": "Validation error or missing required field"
}
```

**401 Unauthorized:**
```json
{
  "message": "Invalid or missing JWT token"
}
```

**404 Not Found:**
```json
{
  "message": "Record not found"
}
```

**500 Internal Server Error:**
```json
{
  "message": "Database error or server issue"
}
```

---

## 💡 Frontend Implementation Example (JavaScript/React)

### 1. Login and Get Token
```javascript
async function login(username, password) {
  const response = await fetch('https://localhost:7046/api/users/login', {
	method: 'POST',
	headers: { 'Content-Type': 'application/json' },
	body: JSON.stringify({ username, password })
  });

  const data = await response.json();
  localStorage.setItem('token', data.token);
  return data.token;
}
```

### 2. Get Presigned URL
```javascript
async function getPresignedUrl(fileName, contentType = 'video/mp4') {
  const token = localStorage.getItem('token');

  const response = await fetch('https://localhost:7046/api/records/video-upload-url', {
	method: 'POST',
	headers: {
	  'Content-Type': 'application/json',
	  'Authorization': `Bearer ${token}`
	},
	body: JSON.stringify({ fileName, contentType })
  });

  return await response.json();
}
```

### 3. Upload to S3
```javascript
async function uploadToS3(presignedUrl, file) {
  const response = await fetch(presignedUrl, {
	method: 'PUT',
	headers: { 'Content-Type': file.type },
	body: file
  });

  return response.ok;
}
```

### 4. Save Record to Backend
```javascript
async function createRecord(recordData) {
  const token = localStorage.getItem('token');

  const response = await fetch('https://localhost:7046/api/records', {
	method: 'POST',
	headers: {
	  'Content-Type': 'application/json',
	  'Authorization': `Bearer ${token}`
	},
	body: JSON.stringify(recordData)
  });

  return await response.json();
}
```

### 5. Full Upload Workflow
```javascript
async function uploadVideoRecord(file, recordInfo) {
  try {
	// Step 1: Get presigned URL
	const presignedUrlData = await getPresignedUrl(file.name, file.type);

	// Step 2: Upload to S3
	await uploadToS3(presignedUrlData.uploadUrl, file);

	// Step 3: Save record to database
	const record = await createRecord({
	  ...recordInfo,
	  videoUrl: presignedUrlData.publicUrl
	});

	console.log('Upload successful!', record);
	return record;
  } catch (error) {
	console.error('Upload failed:', error);
  }
}
```

---

## 🔍 Testing with Swagger

Visit: `https://localhost:7046/swagger/index.html`

All endpoints are documented and testable directly in the browser!

---

## 📝 Notes

- **Response Structure:** All record endpoints return `RecordResponseDto` (DTO, not Entity) for clean API contracts
- **Nested Objects:** User, Map, Vehicle, and GameMode are included as minimal DTOs (not full entities)
- **FinishTime:** Returned as seconds (double), converted from TimeSpan on backend
- **JWT Token Expiration:** Check token validity before each request
- **CORS:** Configure CORS if Frontend is on different domain
- **File Size Limits:** Presigned URLs expire (check `expiresAtUtc`)
- **Video Formats:** Recommended: MP4, WebM
- **Thumbnail URLs:** Optional but recommended for better UX

---

**For support or questions, contact the backend team!** 🚀
