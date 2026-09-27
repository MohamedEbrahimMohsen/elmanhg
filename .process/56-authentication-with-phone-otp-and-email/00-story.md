# [E1.S3] Authentication with phone OTP and email

Issue: #56

As a student I can sign up and log in with my phone number or email so that my progress is saved. PRD §7.1.

Epic: #53

### Sub-tasks
- [ ] User aggregate: id, role, phone, email, display name, status
- [ ] Register and login commands with OTP provider abstraction (SMS gateway to be chosen)
- [ ] JWT issuance with refresh tokens and role claims
- [ ] Rate limiting on OTP requests and login attempts
- [ ] Frontend: sign up, OTP verify, login, logout screens
