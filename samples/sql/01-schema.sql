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
	CONSTRAINT fk_offerings_course FOREIGN KEY (course_id) REFERENCES courses(id),
	CONSTRAINT fk_offerings_teacher FOREIGN KEY (teacher_id) REFERENCES teachers(id),
	CONSTRAINT fk_offerings_term FOREIGN KEY (term_id) REFERENCES terms(id),
	CONSTRAINT uq_offering UNIQUE (course_id, term_id, teacher_id)
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
CREATE INDEX ix_courses_department ON courses(department_id);
CREATE INDEX ix_offerings_course ON course_offerings(course_id);
CREATE INDEX ix_offerings_teacher ON course_offerings(teacher_id);
CREATE INDEX ix_offerings_term ON course_offerings(term_id);
CREATE INDEX ix_enrollments_student ON enrollments(student_id);
CREATE INDEX ix_enrollments_offering ON enrollments(offering_id);
