# MorningBrewCRM - Tổng quan dự án

Tài liệu này tóm tắt các file chính trong repo, cấu trúc database (từ entity classes), API contract (các endpoint chính), JSON mẫu và hướng dẫn Swagger. Nội dung bằng tiếng Việt.

## Tổng quan

MorningBrew.API là service backend (ASP.NET) cung cấp các API cho CRM/B2 khách hàng: quản lý khách hàng, loyalty, promotion, feedback và module khảo sát (surveys). Dùng InMemory EF Core trong Program.cs (phục vụ phát triển).

## File và chức năng chính

- MorningBrew.Api/Program.cs
  - Cấu hình ứng dụng: DI, Authentication (JWT), DbContext (InMemory), Swagger, middleware, seed data.

- MorningBrew.Api/Data/DbInitializer.cs
  - Seed dữ liệu mẫu (Roles, Area, Branch, Users, Customer) khi khởi chạy.

- MorningBrew.Api/Domain/Entities/*.cs
  - Các entity domain mapping tới bảng DB (EF Core). Các file chính:
	- Survey.cs: metadata khảo sát (Id, Title, Description, StartAt, EndAt, IsActive, CreatedBy, CreatedAt). Có navigation collection Questions.
	- SurveyQuestion.cs: câu hỏi (QuestionKey, QuestionText, QuestionType, OptionsJSON, DisplayOrder).
	- SurveyRecipient.cs: khảo sát gán cho khách (SurveyId, CustomerId, Status, AssignedAt, CompletedAt).
	- SurveyResponse.cs: header một lần submit của khách (SurveyId, CustomerId, CreatedAt).
	- SurveyAnswer.cs: từng câu trả lời (SurveyResponseId, QuestionKey, Answer).
	- Các entity khác: AppUser, Area, Branch, Customer, Promotion, Feedback, LoyaltyTransaction, Role, CustomerPromotion (xem file tương ứng để chi tiết).

- MorningBrew.Api/DTOs/
  - DTOs cho request/response giữa client và API. File chính:
	- DTOs/Surveys/SurveyDtos.cs: CreateQuestionRequest, CreateSurveyRequest, SurveyQuestionResponse, SurveyResponse (DTO), SubmitAnswerRequest, SubmitSurveyRequest, SurveyAnalyticsResponse.
	- Các DTOs khác cho Auth, Customers, Feedbacks, Promotions.

- MorningBrew.Api/Controllers/*
  - API endpoints. Một vài controller quan trọng:
	- SurveysController.cs (route: api/surveys)
	  - GET api/surveys/active (Authorized): lấy khảo sát active cho khách.
	  - POST api/surveys/{id}/submit (Authorized): nộp khảo sát.

	- AdminSurveysController.cs (route: api/admin/surveys) [Authorize Roles Admin,Owner]
	  - POST api/admin/surveys: tạo khảo sát (CreateSurveyRequest)
	  - POST api/admin/surveys/send/{surveyId}: gửi khảo sát tới danh sách khách (body: array of GUID)
	  - GET api/admin/surveys/{id}/analytics: lấy analytics (SurveyAnalyticsResponse)

	- AdminFeedbacksController.cs (route: api/admin/adminfeedbacks) [Authorize roles]
	  - POST list, PUT {id}/reply (xem file để chi tiết)

- MorningBrew.Api/Services/Interfaces/*.cs và Implementations/*.cs
  - Các service chứa logic nghiệp vụ. Ví dụ ISurveyService + SurveyService implements:
	- CreateSurveyAsync(CreateSurveyRequest)
	- SendSurveyToCustomersAsync(Guid, IEnumerable<Guid>)
	- GetSurveyAnalyticsAsync(Guid)
	- GetActiveSurveysAsync(Guid customerId)
	- SubmitSurveyAsync(Guid, SubmitSurveyRequest)

## Cấu trúc Database (từ các Entity classes)

Bảng chính liên quan Surveys:

- Surveys
  - Id: GUID (PK)
  - Title: string
  - Description: string?
  - StartAt: DateTime
  - EndAt: DateTime
  - IsActive: bool
  - CreatedBy: GUID
  - CreatedAt: DateTime

- SurveyQuestions
  - Id: GUID (PK)
  - SurveyId: GUID (FK -> Surveys.Id)
  - QuestionKey: string
  - QuestionText: string
  - QuestionType: string (Text, SingleChoice, MultipleChoice, Rating...)
  - OptionsJSON: string? (JSON array of options for choice questions)
  - DisplayOrder: int

- SurveyRecipients
  - Id: GUID (PK)
  - SurveyId: GUID (FK)
  - CustomerId: GUID
  - Status: string (Assigned, Completed)
  - AssignedAt: DateTime
  - CompletedAt: DateTime?

- SurveyResponses
  - Id: GUID (PK)
  - SurveyId: GUID (FK)
  - CustomerId: GUID
  - CreatedAt: DateTime

- SurveyAnswers
  - Id: GUID (PK)
  - SurveyResponseId: GUID (FK -> SurveyResponses.Id)
  - QuestionKey: string
  - Answer: string

Các bảng khác: Customers (Customer.cs), AppUser, Promotions, Feedbacks, LoyaltyTransaction, Roles, Branch, Area, CustomerPromotion.

## API Contract (chính liên quan đến Surveys)

1) Lấy khảo sát active cho khách
  - Method: GET
  - Route: /api/surveys/active
  - Auth: Bearer JWT (Authorized)
  - Request: none (customerId được lấy từ Claim NameIdentifier)
  - Response: 200 OK
	- Body: IEnumerable<SurveyResponse> (DTO)
	  - SurveyResponse: { Id, Title, Description, StartAt, EndAt, IsActive, Questions: [ SurveyQuestionResponse ] }
	  - SurveyQuestionResponse: { QuestionKey, QuestionText, QuestionType, OptionsJSON, DisplayOrder }

2) Khách nộp khảo sát
  - Method: POST
  - Route: /api/surveys/{id}/submit
  - Auth: Bearer JWT
  - Request Body: SubmitSurveyRequest
	- { "customerId": "<guid>", "answers": [ { "questionKey": "q1", "answer": "..." }, ... ] }
  - Response: 204 No Content

3) Admin tạo khảo sát
  - Method: POST
  - Route: /api/admin/surveys
  - Auth: Roles Admin,Owner
  - Request Body: CreateSurveyRequest
	- { "title": "...", "description": "...", "startAt": "2026-01-01T00:00:00Z", "endAt": "2026-02-01T00:00:00Z", "questions": [ { "questionKey": "q1", "questionText": "...", "questionType": "SingleChoice", "optionsJSON": "[\"A\",\"B\"]", "displayOrder": 1 } ], "isActive": true }
  - Response: 200 OK
	- Body: { "surveyId": "<guid>" }

4) Admin gửi khảo sát cho danh sách khách
  - Method: POST
  - Route: /api/admin/surveys/send/{surveyId}
  - Auth: Roles Admin,Owner
  - Request Body: [ "guid1", "guid2", ... ]
  - Response: 204 No Content

5) Admin xem analytics
  - Method: GET
  - Route: /api/admin/surveys/{id}/analytics
  - Response: 200 OK
	- Body: SurveyAnalyticsResponse { surveyId, totalRecipients, completedCount, completionRate }

## JSON mẫu

CreateSurveyRequest mẫu:

{
  "title": "Khảo sát hài lòng khách hàng",
  "description": "Survey về trải nghiệm",
  "startAt": "2026-09-01T00:00:00Z",
  "endAt": "2026-09-30T23:59:59Z",
  "questions": [
	{ "questionKey": "q1", "questionText": "Bạn đánh giá dịch vụ thế nào?", "questionType": "SingleChoice", "optionsJSON": "[\"Tốt\",\"Trung Bình\",\"Kém\"]", "displayOrder": 1 }
  ],
  "isActive": true
}

SubmitSurveyRequest mẫu:

{
  "customerId": "ffffffff-ffff-ffff-ffff-ffffffffffff",
  "answers": [ { "questionKey": "q1", "answer": "Tốt" } ]
}

Send survey (list of customer ids) mẫu:

[ "ffffffff-ffff-ffff-ffff-ffffffffffff", "00000000-0000-0000-0000-000000000000" ]

SurveyAnalyticsResponse mẫu:

{
  "surveyId": "<guid>",
  "totalRecipients": 100,
  "completedCount": 40,
  "completionRate": 40.0
}

## Swagger / thử nghiệm

- Swagger UI được bật trong Program.cs tại /swagger. Mở `https://<host>/swagger` khi ứng dụng chạy để thử các endpoint.
- Postman / HTTP file: MorningBrew.Api/MorningBrew.Api.http có một số request mẫu.

## Gợi ý cải thiện (tùy chọn)

- Đổi tên entity domain SurveyResponse (bản ghi response) thành SurveySubmission hoặc SurveySubmissionHeader để tránh trùng tên với DTO SurveyResponse.
- Thêm DataAnnotations / validation attributes vào DTO để đảm bảo dữ liệu vào hợp lệ.
- Nếu muốn persist, chuyển từ InMemoryDatabase sang một provider thực (SqlServer/Postgres) và tạo migrations.

---

Nếu bạn muốn, tôi có thể:
- mở rộng README với sơ đồ ER (dạng ASCII hoặc mermaid),
- sinh swagger.json từ controllers hoặc tạo file OpenAPI chi tiết,
- hoặc đổi tên domain type để tránh nhầm lẫn loại.
