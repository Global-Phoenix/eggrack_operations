-- Managed dictionaries for sample files and procurement costs.
CREATE TABLE IF NOT EXISTS procurement_file_types (
 id INT UNSIGNED NOT NULL AUTO_INCREMENT,type_code VARCHAR(60) NOT NULL,type_name VARCHAR(120) NOT NULL,
 description VARCHAR(500) DEFAULT NULL,allowed_extensions VARCHAR(500) DEFAULT NULL,max_file_size_mb INT UNSIGNED NOT NULL DEFAULT 50,
 is_active TINYINT(1) NOT NULL DEFAULT 1,sort_order INT UNSIGNED NOT NULL DEFAULT 0,created_at INT UNSIGNED NOT NULL,updated_at INT UNSIGNED NOT NULL,
 PRIMARY KEY(id),UNIQUE KEY uk_procurement_file_type_code(type_code),KEY idx_procurement_file_type_active(is_active,sort_order)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 ROW_FORMAT=DYNAMIC;

CREATE TABLE IF NOT EXISTS procurement_cost_types (
 id INT UNSIGNED NOT NULL AUTO_INCREMENT,type_code VARCHAR(60) NOT NULL,type_name VARCHAR(120) NOT NULL,
 description VARCHAR(500) DEFAULT NULL,is_active TINYINT(1) NOT NULL DEFAULT 1,sort_order INT UNSIGNED NOT NULL DEFAULT 0,
 created_at INT UNSIGNED NOT NULL,updated_at INT UNSIGNED NOT NULL,
 PRIMARY KEY(id),UNIQUE KEY uk_procurement_cost_type_code(type_code),KEY idx_procurement_cost_type_active(is_active,sort_order)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 ROW_FORMAT=DYNAMIC;

ALTER TABLE purchase_plan_files ADD COLUMN file_type_id INT UNSIGNED DEFAULT NULL AFTER file_type,ADD KEY idx_purchase_plan_files_type(file_type_id,status);
ALTER TABLE procurement_plan_cost_items ADD COLUMN cost_type_id INT UNSIGNED DEFAULT NULL AFTER plan_id,ADD KEY idx_procurement_plan_cost_type(cost_type_id);

INSERT INTO procurement_file_types(type_code,type_name,description,allowed_extensions,max_file_size_mb,is_active,sort_order,created_at,updated_at) VALUES
('sample_photo','样品照片','样品外观、细节和包装照片','jpg,jpeg,png,webp',20,1,10,UNIX_TIMESTAMP(),UNIX_TIMESTAMP()),
('sample_video','样品视频','样品功能、操作或验货视频','mp4,webm,mov',50,1,20,UNIX_TIMESTAMP(),UNIX_TIMESTAMP()),
('inspection','检验资料','检验报告、规格书或测试资料','pdf,jpg,jpeg,png,xls,xlsx',50,1,30,UNIX_TIMESTAMP(),UNIX_TIMESTAMP()),
('other','其他附件','与样品相关的其他文件',NULL,50,1,99,UNIX_TIMESTAMP(),UNIX_TIMESTAMP())
ON DUPLICATE KEY UPDATE type_name=VALUES(type_name),description=VALUES(description),allowed_extensions=VALUES(allowed_extensions),max_file_size_mb=VALUES(max_file_size_mb),is_active=VALUES(is_active),sort_order=VALUES(sort_order),updated_at=VALUES(updated_at);

INSERT INTO procurement_cost_types(type_code,type_name,description,is_active,sort_order,created_at,updated_at) VALUES
('product','产品采购成本','采购产品本身的成本',1,10,UNIX_TIMESTAMP(),UNIX_TIMESTAMP()),
('packaging','包装成本','包装材料和包装加工成本',1,20,UNIX_TIMESTAMP(),UNIX_TIMESTAMP()),
('sample','样品成本','样品采购及寄送成本',1,30,UNIX_TIMESTAMP(),UNIX_TIMESTAMP()),
('domestic_shipping','国内运输','国内段运输成本',1,40,UNIX_TIMESTAMP(),UNIX_TIMESTAMP()),
('international_shipping','国际运输','国际段运输成本',1,50,UNIX_TIMESTAMP(),UNIX_TIMESTAMP()),
('customs','关税及清关','关税、报关和清关成本',1,60,UNIX_TIMESTAMP(),UNIX_TIMESTAMP()),
('other','其他成本','其他可说明的采购成本',1,99,UNIX_TIMESTAMP(),UNIX_TIMESTAMP())
ON DUPLICATE KEY UPDATE type_name=VALUES(type_name),description=VALUES(description),is_active=VALUES(is_active),sort_order=VALUES(sort_order),updated_at=VALUES(updated_at);
