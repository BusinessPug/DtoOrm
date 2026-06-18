SET NAMES utf8mb4;
SET time_zone = '+01:00';

CREATE TABLE departments (
	id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
	code VARCHAR(10) NOT NULL UNIQUE,
	name VARCHAR(100) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE teachers (
	id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
	first_name VARCHAR(100) NOT NULL,
	last_name VARCHAR(100) NOT NULL,
	email VARCHAR(200) NOT NULL UNIQUE,
	department_id INT NOT NULL,
	hired_at DATE NOT NULL,
	is_active TINYINT(1) NOT NULL DEFAULT 1,
	CONSTRAINT fk_teachers_department FOREIGN KEY (department_id) REFERENCES departments(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE students (
	id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
	first_name VARCHAR(100) NOT NULL,
	last_name VARCHAR(100) NOT NULL,
	email VARCHAR(200) NOT NULL UNIQUE,
	date_of_birth DATE NOT NULL,
	enrolled_at DATE NOT NULL,
	is_active TINYINT(1) NOT NULL DEFAULT 1
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE app_users (
	id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
	username VARCHAR(80) NOT NULL UNIQUE,
	email VARCHAR(200) NOT NULL UNIQUE,
	password_hash VARCHAR(255) NOT NULL,
	role VARCHAR(20) NOT NULL,
	display_name VARCHAR(200) NOT NULL,
	student_id INT NULL,
	teacher_id INT NULL,
	is_active TINYINT(1) NOT NULL DEFAULT 1,
	created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
	CONSTRAINT fk_app_users_student FOREIGN KEY (student_id) REFERENCES students(id),
	CONSTRAINT fk_app_users_teacher FOREIGN KEY (teacher_id) REFERENCES teachers(id),
	CONSTRAINT ck_app_users_role CHECK (role IN ('Administrator', 'Teacher', 'Student')),
	CONSTRAINT ck_app_users_profile CHECK (
		(role = 'Student' AND student_id IS NOT NULL AND teacher_id IS NULL) OR
		(role = 'Teacher' AND teacher_id IS NOT NULL AND student_id IS NULL) OR
		(role = 'Administrator' AND student_id IS NULL AND teacher_id IS NULL)
	)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE courses (
	id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
	code VARCHAR(20) NOT NULL UNIQUE,
	title VARCHAR(200) NOT NULL,
	credits INT NOT NULL,
	department_id INT NOT NULL,
	CONSTRAINT fk_courses_department FOREIGN KEY (department_id) REFERENCES departments(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE terms (
	id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
	code VARCHAR(20) NOT NULL UNIQUE,
	name VARCHAR(100) NOT NULL,
	start_date DATE NOT NULL,
	end_date DATE NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE course_offerings (
	id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
	course_id INT NOT NULL,
	teacher_id INT NOT NULL,
	term_id INT NOT NULL,
	capacity INT NOT NULL,
	room VARCHAR(50) NOT NULL,
	notes VARCHAR(1000) NULL,
	CONSTRAINT fk_offerings_course FOREIGN KEY (course_id) REFERENCES courses(id),
	CONSTRAINT fk_offerings_teacher FOREIGN KEY (teacher_id) REFERENCES teachers(id),
	CONSTRAINT fk_offerings_term FOREIGN KEY (term_id) REFERENCES terms(id),
	CONSTRAINT uq_offering UNIQUE (course_id, term_id, teacher_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE course_meetings (
	id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
	offering_id INT NOT NULL,
	day_of_week TINYINT NOT NULL,
	starts_at TIME NOT NULL,
	ends_at TIME NOT NULL,
	location VARCHAR(80) NOT NULL,
	CONSTRAINT fk_meetings_offering FOREIGN KEY (offering_id) REFERENCES course_offerings(id),
	CONSTRAINT ck_meetings_day CHECK (day_of_week BETWEEN 1 AND 7),
	CONSTRAINT ck_meetings_time CHECK (starts_at < ends_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE academic_breaks (
	id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
	term_id INT NOT NULL,
	name VARCHAR(100) NOT NULL,
	start_date DATE NOT NULL,
	end_date DATE NOT NULL,
	notes VARCHAR(255) NULL,
	CONSTRAINT fk_breaks_term FOREIGN KEY (term_id) REFERENCES terms(id),
	CONSTRAINT ck_breaks_dates CHECK (start_date <= end_date)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE enrollments (
	id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
	student_id INT NOT NULL,
	offering_id INT NOT NULL,
	enrolled_at DATE NOT NULL,
	grade VARCHAR(2) NULL,
	CONSTRAINT fk_enrollments_student FOREIGN KEY (student_id) REFERENCES students(id),
	CONSTRAINT fk_enrollments_offering FOREIGN KEY (offering_id) REFERENCES course_offerings(id),
	CONSTRAINT uq_enrollment UNIQUE (student_id, offering_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE INDEX ix_teachers_department ON teachers(department_id);
CREATE INDEX ix_app_users_role ON app_users(role);
CREATE INDEX ix_app_users_student ON app_users(student_id);
CREATE INDEX ix_app_users_teacher ON app_users(teacher_id);
CREATE INDEX ix_courses_department ON courses(department_id);
CREATE INDEX ix_offerings_course ON course_offerings(course_id);
CREATE INDEX ix_offerings_teacher ON course_offerings(teacher_id);
CREATE INDEX ix_offerings_term ON course_offerings(term_id);
CREATE INDEX ix_meetings_offering ON course_meetings(offering_id);
CREATE INDEX ix_breaks_term ON academic_breaks(term_id);
CREATE INDEX ix_enrollments_student ON enrollments(student_id);
CREATE INDEX ix_enrollments_offering ON enrollments(offering_id);
