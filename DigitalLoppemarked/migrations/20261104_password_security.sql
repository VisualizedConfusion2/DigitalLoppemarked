-- Add security_stamp to users
ALTER TABLE users ADD COLUMN security_stamp UUID DEFAULT gen_random_uuid();

-- Create password_history table to prevent reuse
CREATE TABLE password_history (
    user_id INT REFERENCES users(id) ON DELETE CASCADE,
    password_hash TEXT NOT NULL,
    changed_at TIMESTAMP WITH TIME ZONE DEFAULT now(),
    PRIMARY KEY (user_id, password_hash, changed_at)
);

-- Use existing user_verification_tokens for password resets
-- Ensure it has the necessary columns (based on previous discovery)
-- user_id INT, token TEXT, expires_at TIMESTAMP
