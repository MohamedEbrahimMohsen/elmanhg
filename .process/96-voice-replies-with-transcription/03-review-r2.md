# Review r2 (orchestrator), #96

#1 is fixed: per-format valid and spoofed tests (webm, ogg, m4a, mp4); the ogg and mp4 signature mutations are caught. #2 does not apply on Resilience 10.7.0: `AddStandardResilienceHandler` already makes the client timeout infinite, as a probe showed, and an explicit infinite timeout was added anyway. Small notes fixed: % is rejected in public keys, and an abandoned recording is discarded. main (#113, #92) is merged, with observability wired for the new worker. api 3315/3315, ai 196, web 1006. VERDICT: APPROVED
