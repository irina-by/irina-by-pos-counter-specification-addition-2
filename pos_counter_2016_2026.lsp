;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; POS COUNTER v4.0 - Universal Version for AutoCAD 2016-2026
;; АВТОР: CAD Department
;; ВЕРСИЯ: 4.0 (Универсальная)
;; ДАТА: 2024.01.30
;; ПОДДЕРЖКА: AutoCAD 2016, 2017, 2018, 2019, 2020, 2021, 2022, 2023, 2024, 2025, 2026
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

(vl-load-com)
(princ "\n[POS] Загрузка POS Counter v4.0... ")

;; ===================================================
;; ГЛОБАЛЬНЫЕ ПЕРЕМЕННЫЕ И НАСТРОЙКИ
;; ===================================================
(setq *pos-settings* '(
    ("default_layer" . "O. В выноски")
    ("export_path" . "")
    ("last_layer" . "")
    ("format_6" . "6")
    ("format_11" . "11")
    ("auto_open_excel" . "1")
    ("mode" . "layer")
    ("layer_scope" . "current") ;; current - только выбранный слой, all - все слои
    ("view_mode" . "dwg")       ;; dwg - по всему DWG, viewport - по выбранному виду
    ("acad_version" . "")
))

(setq *pos-results* nil)
(setq *pos-current-layer* nil)
(setq *pos-total-count* 0)
(setq *pos-dialog-id* nil)
(setq *pos-exported-file* nil)
(setq *pos-open-file-flag* nil)
(setq *pos-dcl-file* "pos_counter.dcl")

;; Определяем версию AutoCAD
(setq *acad-version* (getvar "ACADVER"))
(if (and *acad-version* (> (strlen *acad-version*) 1))
  (progn
(setq *acad-major-version* (atoi (substr *acad-version* 1 2)))
  )
  (progn
    (setq *acad-major-version* 25)
  )
)
(if (and *acad-version* (> (strlen *acad-version*) 0))
(princ (strcat "\n[POS] Версия AutoCAD: " *acad-version*))
  (princ "\n[POS] Версия AutoCAD: Неизвестна")
)

;; ===================================================
;; 0. СОЗДАНИЕ DCL ФАЙЛА (УНИВЕРСАЛЬНЫЙ) - ДОЛЖНА БЫТЬ ПЕРВОЙ
;; ===================================================
(defun create_dcl_file (dcl_path / f result)
  ;; Если путь не передан, используем текущую директорию чертежа
  (if (not dcl_path)
    (progn
      (setq dcl_path (getvar "DWGPREFIX"))
      (if (not dcl_path)
        (setq dcl_path (getvar "ACADPREFIX"))
      )
      (if (not dcl_path)
        (setq dcl_path "")
      )
      ;; Преобразуем в строку на случай если это не строка
      (if (not (= (type dcl_path) 'STR))
        (setq dcl_path (vl-princ-to-string dcl_path))
      )
      ;; Добавляем слеш если его нет
      (if (and (> (strlen dcl_path) 0) 
               (not (wcmatch dcl_path "*\\")))
        (setq dcl_path (strcat dcl_path "\\"))
      )
      ;; Проверяем, что *pos-dcl-file* не nil
      (if (not *pos-dcl-file*)
        (setq *pos-dcl-file* "pos_counter.dcl")
      )
      (setq dcl_path (strcat dcl_path *pos-dcl-file*))
    )
  )
  
  ;; Проверяем, что путь не nil перед открытием файла
  (if (or (not dcl_path) (= dcl_path ""))
    (progn
      (princ "\n[POS] [ERROR] Ошибка: Путь к DCL файлу не определен!")
      nil
    )
    (progn
      (setq f (open dcl_path "w"))
      
      (if (not f)
        (progn
          (princ "\n[POS] [ERROR] Ошибка: Не могу создать DCL файл!")
          nil
        )
        (progn
      ;; Основное окно
      (write-line "// ===========================================" f)
      (write-line "// POS COUNTER v4.0 - Universal Dialog" f)
      (write-line "// For AutoCAD 2016-2026" f)
      (write-line "// ===========================================" f)
      (write-line "" f)
      (write-line "pos_counter : dialog {" f)
      (write-line "    label = \"POS COUNTER v4.0\";" f)
      (write-line "    key = \"title\";" f)
      (write-line "    initial_focus = \"layer_list\";" f)
      (write-line "    spacer;" f)
      (write-line "" f)
      ;; Режим подсчёта по области чертежа: весь DWG или выбранный viewport
      (write-line "    : row {" f)
      (write-line "        : radio_button {" f)
      (write-line "            key = \"view_dwg\";" f)
      (write-line "            label = \"By DWG\";" f)
      (write-line "            value = \"1\";" f)
      (write-line "            width = 15;" f)
      (write-line "        }" f)
      (write-line "        : radio_button {" f)
      (write-line "            key = \"view_viewport\";" f)
      (write-line "            label = \"By viewport\";" f)
      (write-line "            value = \"0\";" f)
      (write-line "            width = 20;" f)
      (write-line "        }" f)
      (write-line "    }" f)
      (write-line "" f)
      ;; Режим подсчёта по способу выбора: по слою или по выделению
      (write-line "    : row {" f)
      (write-line "        : radio_button {" f)
      (write-line "            key = \"mode_layer\";" f)
      (write-line "            label = \"By layer\";" f)
      (write-line "            value = \"1\";" f)
      (write-line "            width = 15;" f)
      (write-line "        }" f)
      (write-line "        : radio_button {" f)
      (write-line "            key = \"mode_selection\";" f)
      (write-line "            label = \"By selection\";" f)
      (write-line "            value = \"0\";" f)
      (write-line "            width = 20;" f)
      (write-line "        }" f)
      (write-line "    }" f)
      (write-line "" f)
      (write-line "    : row {" f)
      (write-line "        : radio_button {" f)
      (write-line "            key = \"scope_current\";" f)
      (write-line "            label = \"Only this layer\";" f)
      (write-line "            value = \"1\";" f)
      (write-line "            width = 20;" f)
      (write-line "        }" f)
      (write-line "        : radio_button {" f)
      (write-line "            key = \"scope_all\";" f)
      (write-line "            label = \"All layers\";" f)
      (write-line "            value = \"0\";" f)
      (write-line "            width = 15;" f)
      (write-line "        }" f)
      (write-line "    }" f)
      (write-line "" f)
      (write-line "    spacer;" f)
      (write-line "" f)
      (write-line "    : row {" f)
      (write-line "        : text {" f)
      (write-line "            label = \"Layer for counting:\";" f)
      (write-line "            width = 20;" f)
      (write-line "        }" f)
      (write-line "        : popup_list {" f)
      (write-line "            key = \"layer_list\";" f)
      (write-line "            width = 30;" f)
      (write-line "        }" f)
      (write-line "    }" f)
      (write-line "" f)
      (write-line "    spacer;" f)
      (write-line "" f)
      (write-line "    : boxed_column {" f)
      (write-line "        label = \"COUNTING RESULTS:\";" f)
      (write-line "        : list_box {" f)
      (write-line "            key = \"results_box\";" f)
      (write-line "            width = 50;" f)
      (write-line "            height = 16;" f)
      (write-line "        }" f)
      (write-line "    }" f)
      (write-line "" f)
      (write-line "    spacer;" f)
      (write-line "" f)
      (write-line "    : row {" f)
      (write-line "        fixed_width = true;" f)
      (write-line "        alignment = centered;" f)
      (write-line "        : button {" f)
      (write-line "            key = \"btn_count\";" f)
      (write-line "            label = \"COUNT\";" f)
      (write-line "            width = 15;" f)
      (write-line "        }" f)
      (write-line "        : spacer { width = 2; }" f)
      (write-line "        : button {" f)
      (write-line "            key = \"btn_export\";" f)
      (write-line "            label = \"EXPORT TO EXCEL\";" f)
      (write-line "            width = 20;" f)
      (write-line "        }" f)
      (write-line "        : spacer { width = 2; }" f)
      (write-line "        : button {" f)
      (write-line "            key = \"cancel\";" f)
      (write-line "            label = \"CLOSE\";" f)
      (write-line "            is_cancel = true;" f)
      (write-line "            width = 10;" f)
      (write-line "        }" f)
      (write-line "    }" f)
      (write-line "" f)
      ;; Кнопка навигации по найденным выноскам
      (write-line "    : row {" f)
      (write-line "        fixed_width = true;" f)
      (write-line "        alignment = centered;" f)
      (write-line "        : button {" f)
      (write-line "            key = \"btn_show\";" f)
      (write-line "            label = \"SHOW ON DRAWING\";" f)
      (write-line "            width = 20;" f)
      (write-line "        }" f)
      (write-line "    }" f)
      (write-line "" f)
      (write-line "    spacer;" f)
      (write-line "" f)
      (write-line "    : text {" f)
      (write-line "        key = \"status\";" f)
      (write-line "        label = \"Ready. Mode: by layer. Select layer and press COUNT\";" f)
      (write-line "        width = 60;" f)
      (write-line "    }" f)
      (write-line "}" f)
      (write-line "" f)
      
      (close f)
      (princ "\n[POS] [OK] DCL файл успешно создан!")
      (setq result t)
      result
        )
      )
    )
  )
)

;; ===================================================
;; 8.0.1. РЕЗЕРВНАЯ ФУНКЦИЯ ДЛЯ СОЗДАНИЯ CSV ФАЙЛА ДЛЯ ВСЕХ СЛОЕВ (ДОЛЖНА БЫТЬ ПЕРЕД export_all_layers_to_excel)
;; ===================================================
(defun export_all_layers_to_csv (all_results filename / f layer_name layer_data layer_results layer_total_items pos count percent item)
  (princ (strcat "\n[POS] Создание CSV файла для всех слоев: " filename))
  
  (setq f (open filename "w"))
  
  (if (not f)
    (progn
      (princ (strcat "\n[POS] [ERROR] Не удалось создать файл: " filename))
      nil
    )
    (progn
      ;; UTF-8 BOM для новых версий
      (if (>= *acad-major-version* 23)
        (progn
          (write-char 239 f)
          (write-char 187 f)
          (write-char 191 f)
        )
      )
      
      ;; Заголовок
      (write-line "Номер позиции;Количество;Слой;Процент" f)
      
      ;; Обрабатываем каждый слой
      (foreach layer_data all_results
        (setq layer_name (car layer_data))
        (setq layer_results (cadr layer_data))
        
        ;; Рассчитываем общее количество для слоя
        (setq layer_total_items (apply '+ (mapcar 'cadr layer_results)))
        (if (= layer_total_items 0)
          (setq layer_total_items 1)
        )
        
        ;; Данные слоя
        (foreach item layer_results
          (setq pos (car item))
          (setq count (cadr item))
          (setq percent (rtos (* 100.0 (/ (float count) layer_total_items)) 2 2))
          
          (write-line (strcat 
            (itoa pos) ";"
            (itoa count) ";"
            layer_name ";"
            percent "%"
          ) f)
        )
        
        ;; Итоговая строка для слоя
        (write-line (strcat 
          "ИТОГО;" 
          (itoa layer_total_items) ";"
          layer_name ";"
          "100.00%"
        ) f)
        
        ;; Пустая строка между слоями
        (write-line "" f)
      )
      
      (close f)
      (princ "\n[POS] [OK] CSV файл для всех слоев успешно создан")
      filename
    )
  )
)

;; ===================================================
;; 8.0. ЭКСПОРТ ВСЕХ СЛОЕВ В EXCEL (ДОЛЖНА БЫТЬ ПЕРЕД on_export_all)
;; ===================================================
(defun export_all_layers_to_excel (all_results save_path / filename excel-app workbook worksheet total_items pos count percent item row_num err header-range total-range layer_name layer_data layer_total_items cell)
  ;; Загружаем Visual LISP COM поддержку
  (vl-load-com)
  
  ;; Убеждаемся, что расширение .xlsx
  (setq filename save_path)
  (if (not (wcmatch filename "*.xlsx"))
    (setq filename (strcat (vl-string-subst "" (vl-filename-extension filename) filename) ".xlsx"))
  )
  
  (princ (strcat "\n[POS] Создание Excel файла для всех слоев: " filename))
  
  ;; Пробуем создать Excel файл через COM
  (setq err (vl-catch-all-apply
    (function
      (lambda (/)
        ;; Создаем объект Excel Application
        (setq excel-app (vlax-create-object "Excel.Application"))
        
        ;; Скрываем Excel
        (vlax-put-property excel-app "Visible" :vlax-false)
        (vlax-put-property excel-app "DisplayAlerts" :vlax-false)
        
        ;; Создаем новую книгу
        (setq workbook (vlax-invoke-method excel-app "Workbooks" "Add"))
        (setq worksheet (vlax-get-property workbook "ActiveSheet"))
        
        ;; Устанавливаем название листа
        (vlax-put-property worksheet "Name" "Все слои")
        
        ;; Записываем общий заголовок (используем Value2)
        (vlax-put-property (vlax-get-property worksheet "Cells" "Item" 1 1) "Value2" "Номер позиции")
        (vlax-put-property (vlax-get-property worksheet "Cells" "Item" 1 2) "Value2" "Количество")
        (vlax-put-property (vlax-get-property worksheet "Cells" "Item" 1 3) "Value2" "Слой")
        (vlax-put-property (vlax-get-property worksheet "Cells" "Item" 1 4) "Value2" "Процент")
        
        ;; Форматируем заголовки (жирный шрифт)
        (setq header-range (vlax-get-property worksheet "Range" "A1:D1"))
        (vlax-put-property (vlax-get-property header-range "Font") "Bold" :vlax-true)
        
        ;; Начинаем со строки 2
        (setq row_num 2)
        
        ;; Обрабатываем каждый слой
        (foreach layer_data all_results
          (setq layer_name (car layer_data))
          (setq layer_results (cadr layer_data))
          
          ;; Рассчитываем общее количество для слоя
          (setq layer_total_items (apply '+ (mapcar 'cadr layer_results)))
          (if (= layer_total_items 0)
            (setq layer_total_items 1)
          )
          
          ;; Записываем данные слоя (используем Value2 - все как строки)
          (foreach item layer_results
            (setq pos (car item))
            (setq count (cadr item))
            (setq percent (rtos (* 100.0 (/ (float count) layer_total_items)) 2 2))
            
            ;; Все значения записываем как строки через Value2
            (vlax-put-property (vlax-get-property worksheet "Cells" "Item" row_num 1) "Value2" (itoa pos))
            (vlax-put-property (vlax-get-property worksheet "Cells" "Item" row_num 2) "Value2" (itoa count))
            (vlax-put-property (vlax-get-property worksheet "Cells" "Item" row_num 3) "Value2" (vl-princ-to-string layer_name))
            (vlax-put-property (vlax-get-property worksheet "Cells" "Item" row_num 4) "Value2" (strcat percent "%"))
            
            (setq row_num (1+ row_num))
          )
          
          ;; Записываем ИТОГО для слоя (жирным)
          (vlax-put-property (vlax-get-property worksheet "Cells" "Item" row_num 1) "Value2" "ИТОГО")
          (vlax-put-property (vlax-get-property worksheet "Cells" "Item" row_num 2) "Value2" (itoa layer_total_items))
          (vlax-put-property (vlax-get-property worksheet "Cells" "Item" row_num 3) "Value2" (vl-princ-to-string layer_name))
          (vlax-put-property (vlax-get-property worksheet "Cells" "Item" row_num 4) "Value2" "100.00%")
          
          ;; Форматируем итоговую строку (жирный шрифт)
          (setq total-range (vlax-get-property worksheet "Range" (strcat "A" (itoa row_num) ":D" (itoa row_num))))
          (vlax-put-property (vlax-get-property total-range "Font") "Bold" :vlax-true)
          
          (setq row_num (1+ row_num))  ;; Пустая строка между слоями
        )
        
        ;; Автоматически подбираем ширину столбцов
        (vlax-invoke-method (vlax-get-property worksheet "Columns") "AutoFit")
        
        ;; Сохраняем файл
        (vlax-invoke-method workbook "SaveAs" filename 51)  ;; 51 = xlOpenXMLWorkbook (.xlsx)
        
        ;; Закрываем книгу
        (vlax-invoke-method workbook "Close" :vlax-false)
        
        ;; Закрываем Excel
        (vlax-invoke-method excel-app "Quit")
        
        ;; Освобождаем объекты
        (vlax-release-object worksheet)
        (vlax-release-object workbook)
        (vlax-release-object excel-app)
        
        (princ "\n[POS] [OK] Excel файл для всех слоев успешно создан")
        t
      )
    )
  ))
  
  ;; Проверяем результат
  (if (vl-catch-all-error-p err)
    (progn
      (princ (strcat "\n[POS] [ERROR] Ошибка при создании Excel файла: " (vl-catch-all-error-message err)))
      (princ "\n[POS] [INFO] Пробую создать CSV файл как резервный вариант...")
      
      ;; Резервный вариант - создаем CSV файл
      (setq filename (vl-string-subst ".csv" ".xlsx" filename))
      (setq filename (export_all_layers_to_csv all_results filename))
      
      ;; Если CSV создан успешно, возвращаем имя файла
      (if (and filename (findfile filename))
        filename
        nil
      )
    )
    (progn
      ;; Проверяем, что файл создан успешно
      (if (findfile filename)
        (progn
          (princ (strcat "\n[POS] [OK] Файл успешно создан: " filename))
          filename
        )
        (progn
          (princ (strcat "\n[POS] [WARNING] Файл создан, но не найден: " filename))
          nil
        )
      )
    )
  )
)

;; ===================================================
;; 8.0.2. ВСПОМОГАТЕЛЬНЫЕ ФУНКЦИИ ДЛЯ ЭКСПОРТА (ДОЛЖНЫ БЫТЬ ПЕРЕД c:POSC)
;; ===================================================
(defun ask_open_file (/ answer)
  ;; Спрашиваем пользователя, открыть ли файл
  (initget "Да Нет")
  (setq answer (getkword "\nОткрыть файл после сохранения? [Да/Нет] <Да>: "))
  (if (not answer)
    (setq answer "Да")  ;; По умолчанию "Да"
  )
  (= answer "Да")
)

(defun open_file_safely (filename / err)
  ;; Безопасно открываем файл через Windows Shell
  (princ (strcat "\n[POS] Файл открывается: " filename))
  (setq err (vl-catch-all-apply
    '(lambda ()
       (startapp "cmd.exe" (strcat "/c start \"\" \"" filename "\""))
     )
  ))
  (if (vl-catch-all-error-p err)
    (progn
      (princ (strcat "\n[POS] [WARNING] Не удалось открыть файл: " (vl-catch-all-error-message err)))
      nil
    )
    t
  )
)

;; ===================================================
;; 8.1. ЭКСПОРТ ВСЕХ СЛОЕВ (ДОЛЖНА БЫТЬ ПЕРЕД c:POSC)
;; ===================================================
(defun on_export_all (/ default_filename save_path filename layers layer_name layer_results all_results)
  (set_tile "status" "Подготовка к экспорту всех слоев...")
  
  ;; Получаем все слои с текстами
  (setq layers (get_all_layers))
  
  (if (null layers)
    (progn
      (set_tile "status" "[ERROR] Слои не найдены!")
      (alert "[ERROR] Не найдено слоев с текстами!")
      nil
    )
    (progn
      (set_tile "status" (strcat "Обработка " (itoa (length layers)) " слоев..."))
      
      ;; Создаем имя файла по умолчанию
      (setq default_filename (strcat 
        "POS_Все_слои_"
        (menucmd "M=$(edtime,$(getvar,date),YYYYMMDD_HHMM)")
        "_ACAD"
        (vl-string-translate "." "" *acad-version*)
        ".xlsx"
      ))
      
      ;; Показываем диалог выбора файла
      (setq save_path (getfiled "Сохранить результаты всех слоев в Excel" default_filename "xlsx" 1))
      
      (if save_path
        (progn
          (set_tile "status" "Экспорт всех слоев...")
          
          ;; Собираем результаты по всем слоям
          (setq all_results '())
          (foreach layer_name layers
            (set_tile "status" (strcat "Обработка слоя: " layer_name "..."))
            (setq layer_results (count_positions layer_name))
            (if layer_results
              (setq all_results (cons (list layer_name layer_results) all_results))
            )
          )
          
          ;; Экспортируем все результаты
          (if all_results
            (progn
              (setq filename (export_all_layers_to_excel all_results save_path))
              
              ;; Проверяем, был ли создан файл (Excel или CSV)
              (if (and filename (findfile filename))
                (progn
                  (set_tile "status" "[OK] Экспорт всех слоев завершен")
                  
                  ;; Сохраняем имя файла для использования после закрытия диалога
                  (setq *pos-exported-file* filename)
                  (setq *pos-open-file-flag* t)
                  
                  (alert (strcat 
                    "[OK] ЭКСПОРТ ВСЕХ СЛОЕВ ВЫПОЛНЕН!\n\n"
                    "Обработано слоев: " (itoa (length all_results)) "\n"
                    "Файл сохранен:\n"
                    filename "\n\n"
                    "После закрытия окна будет предложено открыть файл."
                  ))
                  
                  filename
                )
                (progn
                  (set_tile "status" "[ERROR] Ошибка при экспорте")
                  (alert "[ERROR] Не удалось создать файл экспорта!")
                  nil
                )
              )
            )
            (progn
              (set_tile "status" "[ERROR] Нет данных для экспорта")
              (alert "[ERROR] Не найдено данных для экспорта!")
              nil
            )
          )
        )
        (progn
          (set_tile "status" "Экспорт отменен")
          nil
        )
      )
    )
  )
)

;; ===================================================
;; 1. ГЛАВНАЯ КОМАНДА С ГРАФИЧЕСКИМ ИНТЕРФЕЙСОМ
;; ===================================================
(defun c:POSC (/ dcl_id dcl_path result dlg_ret ss_sel layout_name cvport scope layer_name ss_vp vp_handle base_results fallback_all)
  (princ "\n[POS] Запуск POS Counter v4.0...")
  
  ;; Обновляем настройки с версией AutoCAD
  (setq *pos-settings* 
    (subst (cons "acad_version" *acad-version*) 
           (assoc "acad_version" *pos-settings*) 
           *pos-settings*))
  
  ;; Определяем путь к DCL: приоритет — найденный файл, иначе папка чертежа
  (setq dcl_path (findfile *pos-dcl-file*))
  (if (not dcl_path)
    (progn
      (setq dcl_path (getvar "DWGPREFIX"))
      (if (not dcl_path) (setq dcl_path (getvar "ACADPREFIX")))
      (if (not dcl_path) (setq dcl_path "."))
      (setq dcl_path (vl-princ-to-string dcl_path))
      (if (and (> (strlen dcl_path) 0) (not (wcmatch dcl_path "*\\")))
        (setq dcl_path (strcat dcl_path "\\"))
      )
      (if (not *pos-dcl-file*) (setq *pos-dcl-file* "pos_counter.dcl"))
      (setq dcl_path (strcat dcl_path *pos-dcl-file*))
    )
  )
  ;; Всегда перезаписываем DCL нашим содержимым (By viewport, By selection и т.д.)
  (princ "\n[POS] Обновление DCL...")
  (setq result (create_dcl_file dcl_path))
  (if (not result)
    (princ "\n[POS] [WARNING] Не удалось обновить DCL, используется существующий файл.")
  )
  
  (if (or (not dcl_path) (= dcl_path ""))
    (progn
      (alert "[ERROR] Ошибка: Не могу определить путь к DCL файлу")
      (princ "\n[POS] [ERROR] Не могу определить путь к DCL файлу")
      (princ)
      (exit)
    )
  )
  
  ;; Отладочный вывод
  (princ (strcat "\n[POS] Загрузка DCL из: " dcl_path))
  
  ;; Финальная проверка перед загрузкой
  (if (or (not dcl_path) (= (type dcl_path) 'SYM))
    (progn
      (alert "[ERROR] Критическая ошибка: dcl_path равен nil")
      (princ "\n[POS] [ERROR] Критическая ошибка: dcl_path равен nil")
      (princ)
      (exit)
    )
  )
  
  ;; Загружаем диалог
  (setq dcl_id (load_dialog dcl_path))
  
  (if (<= dcl_id 0)
    (progn
      (if *pos-dcl-file*
        (alert (strcat "[ERROR] Ошибка: Не могу загрузить файл " *pos-dcl-file*))
        (alert "[ERROR] Ошибка: Не могу загрузить DCL файл")
      )
      (princ "\n[POS] [ERROR] Ошибка загрузки DCL файла")
    )
    (progn
  ;; Инициализируем диалог
  (if (not (new_dialog "pos_counter" dcl_id))
    (progn
          (alert "[ERROR] Ошибка: Не могу создать диалоговое окно")
      (unload_dialog dcl_id)
    )
        (progn
  ;; Настраиваем диалог
  (setup_dialog)
  
  ;; Назначаем обработчики
  (action_tile "layer_list" "(on_layer_select $value)")
  (action_tile "view_dwg" "(on_viewmode_change)")
  (action_tile "view_viewport" "(on_viewmode_change)")
  (action_tile "mode_layer" "(on_mode_change)")
  (action_tile "mode_selection" "(on_mode_change)")
  (action_tile "btn_count" "(on_count)")
  (action_tile "btn_export" "(on_export)")
  (action_tile "scope_current" "(on_scope_change)")
  (action_tile "scope_all" "(on_scope_change)")
  (action_tile "btn_show" "(on_show)")
  (action_tile "cancel" "(done_dialog 0)")
  
  ;; Сохраняем ID диалога
  (setq *pos-dialog-id* dcl_id)
  
  ;; Запускаем диалог (возврат 3 = By viewport + By selection: запросить выделение)
  (setq dlg_ret (start_dialog))
  
  ;; Очищаем
  (unload_dialog dcl_id)
  (setq *pos-dialog-id* nil)
  
  ;; Возврат 3: запросить выделение. Возврат 4: объекты уже выделены (pickfirst), считать сразу.
  (if (or (= dlg_ret 3) (= dlg_ret 4))
    (progn
      (if (= dlg_ret 4)
        (setq ss_sel (ssget "I"))
        (progn
          (princ "\n[POS] Активируйте viewport (двойной щелчок внутрь), выделите объекты для подсчета...")
          (setq ss_sel (ssget))
        )
      )
      (if (not ss_sel)
        (princ "\n[POS] Выбор отменен. Запустите POSC снова при необходимости.")
        (progn
          (setq layout_name (getvar "CTAB"))
          (setq cvport (getvar "CVPORT"))
          (setq scope (cdr (assoc "layer_scope" *pos-settings*)))
          (if (or (not scope) (= scope "")) (setq scope "current"))
          (setq layer_name *pos-current-layer*)
          ;; Handle текущего viewport
          (setq ss_vp (ssget "_X" (list (cons 0 "VIEWPORT") (cons 69 cvport) (cons 410 layout_name))))
          (setq vp_handle (if (and ss_vp (> (sslength ss_vp) 0))
                            (cdr (assoc 5 (entget (ssname ss_vp 0))))
                            "?"))
          (setq fallback_all nil)
          (setq base_results (count_positions_from_ss ss_sel layer_name scope))
          ;; Если по выбранному слою пусто — пробуем «все слои»
          (if (not base_results)
            (progn
              (setq base_results (count_positions_from_ss ss_sel layer_name "all"))
              (if base_results
                (progn (setq scope "all") (setq fallback_all t))
              )
            )
          )
          (if base_results
            (progn
              (setq *pos-results* (if (= scope "all")
                                    (list (list layout_name vp_handle base_results))
                                    (list (list layout_name vp_handle layer_name base_results))))
              (princ "\n[POS] Подсчет по выделению выполнен. Открываю окно с результатами...")
              ;; Повторно открываем диалог с результатами
              (setq dcl_id (load_dialog dcl_path))
              (if (and dcl_id (> dcl_id 0))
                (progn
                  (if (new_dialog "pos_counter" dcl_id)
                    (progn
                      (setup_dialog)
                      (display_results_viewports *pos-results*)
                      (set_tile "status" (if fallback_all
                                           "[OK] По выделению: на выбранном слое пусто; показаны все слои"
                                           "[OK] Подсчет по выделению в viewport выполнен"))
                      (action_tile "layer_list" "(on_layer_select $value)")
                      (action_tile "view_dwg" "(on_viewmode_change)")
                      (action_tile "view_viewport" "(on_viewmode_change)")
                      (action_tile "mode_layer" "(on_mode_change)")
                      (action_tile "mode_selection" "(on_mode_change)")
                      (action_tile "btn_count" "(on_count)")
                      (action_tile "btn_export" "(on_export)")
                      (action_tile "scope_current" "(on_scope_change)")
                      (action_tile "scope_all" "(on_scope_change)")
                      (action_tile "btn_show" "(on_show)")
                      (action_tile "cancel" "(done_dialog 0)")
                      (setq *pos-dialog-id* dcl_id)
                      (start_dialog)
                      (unload_dialog dcl_id)
                      (setq *pos-dialog-id* nil)
                    )
                    (unload_dialog dcl_id)
                  )
                )
              )
            )
            (progn
              (princ "\n[POS] В выделении не найдено подходящих выносок (TEXT/MTEXT/блоки с номерами позиций).")
              (alert "В выделении не найдено подходящих выносок. Проверьте, что выделены TEXT/MTEXT или блоки с номерами позиций (целые числа 1-10000), а не только линии/рамки, и при необходимости используйте режим All layers.")
            )
          )
        )
      )
    )
  )
  
  ;; Проверяем, нужно ли открыть файл после экспорта (только если диалог закрыли не кодом 3 или 4)
  (if (and (/= dlg_ret 3) (/= dlg_ret 4))
    (if (and *pos-exported-file* *pos-open-file-flag*)
      (progn
        (if (ask_open_file)
          (progn
            (open_file_safely *pos-exported-file*)
            (princ (strcat "\n[POS] Файл открыт: " *pos-exported-file*))
          )
          (princ (strcat "\n[POS] Файл сохранен: " *pos-exported-file*))
        )
        (setq *pos-exported-file* nil)
        (setq *pos-open-file-flag* nil)
      )
    )
  )
  (princ "\n[POS] Работа завершена")
  (princ)
        )
      )
    )
  )
)

;; ===================================================
;; 2. ПОЛУЧЕНИЕ СПИСКА СЛОЕВ (УНИВЕРСАЛЬНЫЙ) - ДОЛЖНА БЫТЬ ПЕРЕД setup_dialog
;; ===================================================
(defun get_all_layers (/ layers layer_table layer_name all_layers filtered_layers ss)
  (setq all_layers '())
  (setq filtered_layers '())
  
  ;; Метод 1: Через таблицу слоев (работает во всех версиях)
  (setq layer_table (tblnext "LAYER" t))
  (while layer_table
    (setq layer_name (cdr (assoc 2 layer_table)))
    (setq all_layers (cons layer_name all_layers))
    (setq layer_table (tblnext "LAYER"))
  )
  
  ;; Если список пустой, добавляем хотя бы стандартные слои
  (if (null all_layers)
    (setq all_layers (list "0" "DEFPOINTS"))
  )
  
  ;; Фильтруем: оставляем только слои, на которых есть текстовые объекты
  (foreach layer_name all_layers
    ;; Проверяем наличие текстов на слое
    (setq ss (ssget "_X" 
      (list 
        (cons 0 "TEXT")
        (cons 8 layer_name)
      )
    ))
    ;; Если тексты найдены, добавляем слой в список
    (if ss
      (setq filtered_layers (cons layer_name filtered_layers))
    )
  )
  
  ;; Если после фильтрации список пустой, возвращаем все слои (на случай если проверка не сработала)
  (if (null filtered_layers)
    (setq filtered_layers all_layers)
  )
  
  ;; Сортируем по алфавиту
  (setq filtered_layers (vl-sort filtered_layers '<))
  
  ;; Добавляем слой по умолчанию если его нет и он есть в исходном списке
  (if (and (not (member "O. В выноски" filtered_layers)) 
           (member "O. В выноски" all_layers))
    (setq filtered_layers (cons "O. В выноски" filtered_layers))
  )
  
  ;; Возвращаем отфильтрованный список
  filtered_layers
)

;; ===================================================
;; 3. ИЗВЛЕЧЕНИЕ НОМЕРА ПОЗИЦИИ ИЗ ТЕКСТА (ТОЛЬКО ЦЕЛЫЕ ЧИСЛА 1-10000)
;; ===================================================
(defun extract_position_number (txt / clean_txt num_int patterns pattern i char has_invalid)
  ;; Распознаём только целые числа без точек и запятых
  ;; Не считаем: 04.2025, 0,003, +1,1, -1,1, 20x1,9
  (if (or (not txt) (= txt ""))
    nil
    (progn
  ;; Убираем пробелы в начале и конце
  (setq clean_txt (vl-string-trim " \t\n\r" txt))
  
  ;; Список возможных префиксов
  (setq patterns '(
    "Поз." "Поз" "поз." "поз"
    "POS" "Pos" "pos"
    "P" "p"
    "№" "N" "n" "N." "n."
    "Номер" "номер"
    "Марка" "марка"
    "Позиция" "позиция"
    "Item" "item"
    "Pos." "pos."
  ))

  ;; Удаляем префиксы
  (foreach pattern patterns
    (if (wcmatch clean_txt (strcase (strcat pattern "*")))
      (setq clean_txt (vl-string-trim " \t" (substr clean_txt (+ 1 (strlen pattern)))))
    )
  )

  ;; Проверка: строка не должна содержать . , + - x X (отсекаем даты, десятичные, размеры 20x1,9)
  (setq has_invalid nil)
  (setq i 1)
  (while (and (<= i (strlen clean_txt)) (not has_invalid))
    (setq char (substr clean_txt i 1))
    (if (member char '("." "," "+" "-" "x" "X" "х" "Х" "/"))
      (setq has_invalid t)
    )
    (setq i (1+ i))
  )
  
  (if has_invalid
    nil
    ;; Строка должна состоять ТОЛЬКО из цифр
    (if (and (> (strlen clean_txt) 0)
             (vl-every '(lambda (c) (and (>= c 48) (<= c 57))) (vl-string->list clean_txt)))
      (progn
        (setq num_int (atoi clean_txt))
        ;; Диапазон позиций: 1 до 10000
        (if (and (>= num_int 1) (<= num_int 10000))
          num_int
          nil
        )
      )
      nil
    )
  )
    )
  )
)

;; ===================================================
;; 4. ВСПОМОГАТЕЛЬНЫЕ ФУНКЦИИ ПОДСЧЁТА (СЛОИ, БЛОКИ, XREF)
;; ===================================================

;; Получение \"базового\" имени слоя без Xref-префикса \"ИмяXref|Слой\"
(defun pos-get-base-layer (ent_layer / pos)
  (if (and ent_layer (setq pos (vl-string-search "|" ent_layer)))
    (substr ent_layer (+ pos 2))
    ent_layer
  )
)

;; Сравнение слоя объекта с выбранным слоем (только для режима \"только этот слой\")
(defun pos-layer-match (ent_layer current_layer / base_layer)
  (if (and ent_layer current_layer)
    (progn
      (setq base_layer (pos-get-base-layer ent_layer))
      (= base_layer current_layer)
    )
    nil
  )
)

;; Обработка одного текстового значения: разбор номера и добавление в словарь
;; Для scope=\"current\" result_dict = ((pos count) ...)
;; Для scope=\"all\"     result_dict = ((layer_name ( (pos count) ... )) ...)
(defun pos-process-text (txt_layer txt current_layer scope result_dict total
                          / pos_num base_layer layer_pair layer_results existing)
  (if (or (not txt) (= txt ""))
    (list result_dict total)
    (progn
      (setq pos_num (extract_position_number txt))
      (if (not pos_num)
        (list result_dict total)
        (cond
          ;; Режим: только выбранный слой
          ((or (not scope) (= scope "current"))
           (if (not (pos-layer-match txt_layer current_layer))
             (list result_dict total)
             (progn
               (if (setq existing (assoc pos_num result_dict))
                 (setq result_dict (subst (list pos_num (1+ (cadr existing))) existing result_dict))
                 (setq result_dict (cons (list pos_num 1) result_dict))
               )
               (setq total (1+ total))
               (list result_dict total)
             )
           )
          )
          ;; Режим: все слои — группируем по базовому имени слоя
          ((= scope "all")
           (setq base_layer (pos-get-base-layer txt_layer))
           (if (not base_layer) (setq base_layer ""))
           (setq layer_pair    (assoc base_layer result_dict))
           (setq layer_results (if layer_pair (cadr layer_pair) '()))
           (setq existing      (assoc pos_num layer_results))
           (if existing
             (setq layer_results (subst (list pos_num (1+ (cadr existing)))
                                        existing
                                        layer_results))
             (setq layer_results (cons (list pos_num 1) layer_results))
           )
           (if layer_pair
             (setq result_dict (subst (list base_layer layer_results) layer_pair result_dict))
             (setq result_dict (cons (list base_layer layer_results) result_dict))
           )
           (setq total (1+ total))
           (list result_dict total)
          )
          (t (list result_dict total))
        )
      )
    )
  )
)

;; Рекурсивный обход содержимого блока (включая вложенные блоки)
;; insert_layer — эффективный слой вставки (для объектов на слое 0 внутри блока)
(defun pos-count-texts-in-block (blk_name insert_layer current_layer scope result_dict total
                             / btr ent ed type txt_layer txt res nested_layer nested_res)
  (if (and blk_name (setq btr (tblobjname "BLOCK" blk_name)))
    (progn
      (setq ent (entnext btr))
      (while ent
        (setq ed (entget ent))
        (setq type (cdr (assoc 0 ed)))
        (cond
          ;; Обычный TEXT внутри определения блока
          ((= type "TEXT")
           (setq txt_layer (cdr (assoc 8 ed)))
           (if (= txt_layer "0")
             (setq txt_layer insert_layer)
           )
           (setq txt (cdr (assoc 1 ed)))
           (setq res (pos-process-text txt_layer txt current_layer scope result_dict total))
           (setq result_dict (car res)
                 total      (cadr res))
          )
          ;; MTEXT внутри блока
          ((= type "MTEXT")
           (setq txt_layer (cdr (assoc 8 ed)))
           (if (= txt_layer "0")
             (setq txt_layer insert_layer)
           )
           (setq txt (cdr (assoc 1 ed)))
           (setq res (pos-process-text txt_layer txt current_layer scope result_dict total))
           (setq result_dict (car res)
                 total      (cadr res))
          )
          ;; Атрибуты / определения атрибутов внутри блока
          ((or (= type "ATTRIB") (= type "ATTDEF"))
           (setq txt_layer (cdr (assoc 8 ed)))
           (if (= txt_layer "0")
             (setq txt_layer insert_layer)
           )
           (setq txt (cdr (assoc 1 ed)))
           (setq res (pos-process-text txt_layer txt current_layer scope result_dict total))
           (setq result_dict (car res)
                 total      (cadr res))
          )
          ;; Вложенный INSERT внутри блока
          ((= type "INSERT")
           (setq nested_layer (cdr (assoc 8 ed)))
           (if (= nested_layer "0")
             (setq nested_layer insert_layer)
           )
           (setq nested_res (pos-count-texts-in-block
                              (cdr (assoc 2 ed))
                              nested_layer
                              current_layer
                              scope
                              result_dict
                              total))
           (setq result_dict (car nested_res)
                 total      (cadr nested_res))
          )
        )
        (setq ent (entnext ent))
      )
    )
  )
  (list result_dict total)
)

;; ===================================================
;; 4.1. ВСПОМОГАТЕЛЬНЫЕ ФУНКЦИИ ДЛЯ РЕЖИМА VIEWPORT
;; ===================================================

;; Получение активного viewport на текущем листе БЕЗ интерактивного выбора
;; Требование: пользователь заранее активирует нужное видовое окно (двойной щелчок внутри).
;; Возвращает: (viewport_ent layout_name cvport handle) или nil если подходящий viewport не найден.
(defun pos-select-viewport (/ layout_name cvport ss vp_ent ed handle)
  (setq layout_name (getvar "CTAB"))
  (setq cvport      (getvar "CVPORT"))
  ;; В пространстве листа активный viewport имеет CVPORT > 1
  (if (or (<= cvport 1) (not layout_name))
    (progn
      (princ "\n[POS] [INFO] Активное видовое окно не найдено. Активируйте нужный viewport (двойной щелчок) и повторите подсчет.")
      nil
    )
    (progn
      (setq ss (ssget "_X"
               (list
                 (cons 0 "VIEWPORT")
                 (cons 69 cvport)
                 (cons 410 layout_name))))
      (if (or (null ss) (= (sslength ss) 0))
        (progn
          (princ "\n[POS] [INFO] VIEWPORT с текущим CVPORT не найден на листе.")
          nil
        )
        (progn
          (setq vp_ent (ssname ss 0))
          (setq ed     (entget vp_ent))
          (setq handle (cdr (assoc 5 ed)))
          (list vp_ent layout_name cvport handle)
        )
      )
    )
  )
)

;; Подсчет только по содержимому выбранного viewport
;; Оборачивает стандартный результат count_positions в структуру с листом и viewport
;; Для scope=\"current\": ((layout handle layer_name ((pos count) ...)) ...)
;; Для scope=\"all\"    : ((layout handle ((layer ((pos count) ...)) ...)) ...)
(defun pos-count-in-viewport (layer_name / vp_data vp_ent layout_name cvport handle
                                         old_tilemode old_ctab old_cvport
                                         scope base_results wrapped_results err)
  (setq vp_data (pos-select-viewport))
  (if (not vp_data)
    (progn
      (set_tile "status" "[INFO] Режим по viewport: активное видовое окно не найдено")
      nil
    )
    (progn
      (setq vp_ent      (nth 0 vp_data))
      (setq layout_name (nth 1 vp_data))
      (setq cvport      (nth 2 vp_data))
      (setq handle      (nth 3 vp_data))

      ;; Определяем область подсчета
      (setq scope (cdr (assoc "layer_scope" *pos-settings*)))
      (if (or (not scope) (= scope ""))
        (setq scope "current")
      )

      ;; Сохраняем текущий контекст
      (setq old_tilemode (getvar "TILEMODE"))
      (setq old_ctab     (getvar "CTAB"))
      (setq old_cvport   (getvar "CVPORT"))

      (princ (strcat
               "\n[POS] Режим viewport: лист '"
               (vl-princ-to-string layout_name)
               "', viewport #"
               (itoa cvport)
               ", handle "
               (vl-princ-to-string handle)))

      ;; Переходим на нужный лист и активируем viewport
      (setq err
        (vl-catch-all-apply
          (function
            (lambda ()
              (setvar "TILEMODE" 0)
              (if layout_name (setvar "CTAB" layout_name))
              (setvar "CVPORT" cvport)
              ;; Стандартный подсчет в контексте активного viewport
              (setq base_results (count_positions layer_name))
            )
          )
        )
      )

      ;; Восстанавливаем контекст
      (if old_ctab (setvar "CTAB" old_ctab))
      (if old_tilemode (setvar "TILEMODE" old_tilemode))
      (if old_cvport (setvar "CVPORT" old_cvport))

      (if (vl-catch-all-error-p err)
        (progn
          (princ (strcat "\n[POS] [ERROR] Ошибка при подсчете в viewport: "
                         (vl-catch-all-error-message err)))
          nil
        )
        (if (not base_results)
          (progn
            (princ "\n[POS] [INFO] В выбранном viewport подходящих текстов не найдено.")
            nil
          )
          (progn
            ;; Оборачиваем результаты с привязкой к листу и viewport
            (if (= scope "all")
              (setq wrapped_results (list (list layout_name handle base_results)))
              (setq wrapped_results (list (list layout_name handle layer_name base_results)))
            )
            wrapped_results
          )
        )
      )
    )
  )
)

;; Построение списка объектов для подсветки по результатам подсчета
;; Ограничение: работает в режиме view_mode="dwg" и mode="layer"
;; scope="current": results = ((pos count) ...)
;; scope="all"    : results = ((layer_name ((pos count) ...)) ...)
(defun pos-build-highlight-selection (results current_layer scope
                                     / positions_by_layer positions_list
                                       layer_data layer_name layer_results
                                       txt_positions base_layer layer_entry
                                       highlight_ents ss i ent ed type
                                       txt_layer txt pos_num)
  (setq highlight_ents '())
  ;; Подготавливаем карту позиций по слоям
  (if (= scope "all")
    (progn
      (setq positions_by_layer '())
      (foreach layer_data results
        (setq layer_name    (car layer_data))
        (setq layer_results (cadr layer_data))
        (setq txt_positions (mapcar 'car layer_results))
        (setq positions_by_layer (cons (cons layer_name txt_positions) positions_by_layer))
      )
    )
    ;; scope="current" — один слой, список позиций общий
    (setq positions_list (mapcar 'car results))
  )
  ;; Вспомогательная проверка, нужно ли подсвечивать объект
  (defun pos-should-highlight (ent_layer txt / pos_num base_layer layer_entry)
    (if (or (not txt) (= txt ""))
      nil
      (progn
        (setq pos_num (extract_position_number txt))
        (if (not pos_num)
          nil
          (cond
            ;; Только выбранный слой
            ((or (not scope) (= scope "current"))
             (and (pos-layer-match ent_layer current_layer)
                  (member pos_num positions_list))
            )
            ;; Все слои, группировка по базовому имени слоя
            ((= scope "all")
             (setq base_layer (pos-get-base-layer ent_layer))
             (if (not base_layer) (setq base_layer ""))
             (setq layer_entry (assoc base_layer positions_by_layer))
             (and layer_entry (member pos_num (cdr layer_entry)))
            )
            (t nil)
          )
        )
      )
    )
  )
  ;; Обходим все TEXT
  (setq ss (ssget "_X" '((0 . "TEXT"))))
  (if ss
    (progn
      (setq i 0)
      (while (< i (sslength ss))
        (setq ent (ssname ss i))
        (setq ed  (entget ent))
        (setq txt_layer (cdr (assoc 8 ed)))
        (setq txt       (cdr (assoc 1 ed)))
        (if (pos-should-highlight txt_layer txt)
          (setq highlight_ents (cons ent highlight_ents))
        )
        (setq i (1+ i))
      )
    )
  )
  ;; Обходим все MTEXT
  (setq ss (ssget "_X" '((0 . "MTEXT"))))
  (if ss
    (progn
      (setq i 0)
      (while (< i (sslength ss))
        (setq ent (ssname ss i))
        (setq ed  (entget ent))
        (setq txt_layer (cdr (assoc 8 ed)))
        (setq txt       (cdr (assoc 1 ed)))
        (if (pos-should-highlight txt_layer txt)
          (setq highlight_ents (cons ent highlight_ents))
        )
        (setq i (1+ i))
      )
    )
  )
  ;; Обходим все ATTRIB (атрибуты блоков)
  (setq ss (ssget "_X" '((0 . "ATTRIB"))))
  (if ss
    (progn
      (setq i 0)
      (while (< i (sslength ss))
        (setq ent (ssname ss i))
        (setq ed  (entget ent))
        (setq txt_layer (cdr (assoc 8 ed)))
        (setq txt       (cdr (assoc 1 ed)))
        (if (pos-should-highlight txt_layer txt)
          (setq highlight_ents (cons ent highlight_ents))
        )
        (setq i (1+ i))
      )
    )
  )
  highlight_ents
)

;; Подсчет позиций только по переданному набору (selection set).
;; Используется при "отложенном выборе": диалог закрывается, пользователь выделяет, затем пересчет.
;; Возвращает тот же формат, что и count_positions: ((pos count)...) или ((layer ((pos count)...))...)
(defun count_positions_from_ss (ss layer_name scope / result_dict total i ent ent_data type
                                  txt_layer txt res blk_name blk_layer blk_res)
  (setq result_dict '())
  (setq total 0)
  (if (not ss)
    nil
    (progn
      (setq i 0)
      (while (< i (sslength ss))
        (setq ent (ssname ss i))
        (setq ent_data (entget ent))
        (setq type (cdr (assoc 0 ent_data)))
        (cond
          ((= type "TEXT")
           (setq txt_layer (cdr (assoc 8 ent_data)))
           (setq txt       (cdr (assoc 1 ent_data)))
           (setq res (pos-process-text txt_layer txt layer_name scope result_dict total))
           (setq result_dict (car res) total (cadr res)))
          ((= type "MTEXT")
           (setq txt_layer (cdr (assoc 8 ent_data)))
           (setq txt       (cdr (assoc 1 ent_data)))
           (setq res (pos-process-text txt_layer txt layer_name scope result_dict total))
           (setq result_dict (car res) total (cadr res)))
          ((= type "ATTRIB")
           (setq txt_layer (cdr (assoc 8 ent_data)))
           (setq txt       (cdr (assoc 1 ent_data)))
           (setq res (pos-process-text txt_layer txt layer_name scope result_dict total))
           (setq result_dict (car res) total (cadr res)))
          ((= type "INSERT")
           (setq blk_name  (cdr (assoc 2 ent_data)))
           (setq blk_layer (cdr (assoc 8 ent_data)))
           (if (= blk_layer "0") (setq blk_layer layer_name))
           (setq blk_res (pos-count-texts-in-block blk_name blk_layer layer_name scope result_dict total))
           (setq result_dict (car blk_res) total (cadr blk_res)))
        )
        (setq i (1+ i))
      )
      (if (= total 0)
        nil
        (if (= scope "all")
          (progn
            (setq result_dict
                  (mapcar '(lambda (pair / ln lr)
                             (setq ln (car pair))
                             (setq lr (cadr pair))
                             (list ln (vl-sort lr '(lambda (a b) (< (car a) (car b))))))
                          result_dict))
            (vl-sort result_dict '(lambda (a b) (< (strcase (car a)) (strcase (car b)))))
          )
          (vl-sort result_dict '(lambda (a b) (< (car a) (car b))))
        )
      )
    )
  )
)

;; ===================================================
;; 4. ПОДСЧЕТ ПОЗИЦИЙ (УЧЁТ ВЫДЕЛЕНИЯ, ВСЕГО ЧЕРТЕЖА, БЛОКОВ/XREF)
;; ===================================================
(defun count_positions (layer_name / mode scope view_mode result_dict total sel_ss sel_done
                                   i ent ent_data type txt_layer txt res
                                   ss_text ss_mtext ss_insert total_objects blk_name blk_layer blk_res)
  (princ (strcat "\n[POS] Поиск текстов для слоя: " layer_name))
  
  ;; Определяем режим подсчета
  (setq mode (cdr (assoc "mode" *pos-settings*)))
  (if (or (not mode) (= mode ""))
    (setq mode "layer")
  )
  
  ;; Определяем область подсчета: только слой / все слои
  (setq scope (cdr (assoc "layer_scope" *pos-settings*)))
  (if (or (not scope) (= scope ""))
    (setq scope "current")
  )
  ;; Определяем режим просмотра (по всему DWG / по viewport)
  (setq view_mode (cdr (assoc "view_mode" *pos-settings*)))
  (if (or (not view_mode) (= view_mode ""))
    (setq view_mode "dwg")
  )
  
  (setq result_dict '())
  (setq total 0)
  (setq sel_done nil)
  
  ;; -------------------------------
  ;; РЕЖИМ \"ПО ВЫДЕЛЕНИЮ\"
  ;; -------------------------------
  (if (= mode "selection")
    (progn
      (setq sel_ss (ssget "I"))
      (if sel_ss
        (progn
          (princ (strcat "\n[POS] Режим: по выделению. Объектов в выделении: " (itoa (sslength sel_ss))))
          (setq i 0)
          (while (< i (sslength sel_ss))
            (setq ent (ssname sel_ss i))
            (setq ent_data (entget ent))
            (setq type (cdr (assoc 0 ent_data)))
            (cond
              ;; Прямые TEXT в выделении
              ((= type "TEXT")
               (setq txt_layer (cdr (assoc 8 ent_data)))
               (setq txt       (cdr (assoc 1 ent_data)))
               (setq res (pos-process-text txt_layer txt layer_name scope result_dict total))
               (setq result_dict (car res)
                     total      (cadr res))
              )
              ;; Прямые MTEXT в выделении
              ((= type "MTEXT")
               (setq txt_layer (cdr (assoc 8 ent_data)))
               (setq txt       (cdr (assoc 1 ent_data)))
               (setq res (pos-process-text txt_layer txt layer_name scope result_dict total))
               (setq result_dict (car res)
                     total      (cadr res))
              )
              ;; Атрибуты в выделении
              ((= type "ATTRIB")
               (setq txt_layer (cdr (assoc 8 ent_data)))
               (setq txt       (cdr (assoc 1 ent_data)))
               (setq res (pos-process-text txt_layer txt layer_name scope result_dict total))
               (setq result_dict (car res)
                     total      (cadr res))
              )
              ;; Вставки блоков / Xref в выделении
              ((= type "INSERT")
               (setq blk_name  (cdr (assoc 2 ent_data)))
               (setq blk_layer (cdr (assoc 8 ent_data)))
               (if (= blk_layer "0")
                 (setq blk_layer layer_name) ;; если блок на нулевом слое, считаем как выбранный
               )
               (setq blk_res (pos-count-texts-in-block
                               blk_name
                               blk_layer
                               layer_name
                               scope
                               result_dict
                               total))
               (setq result_dict (car blk_res)
                     total      (cadr blk_res))
              )
            )
            (setq i (1+ i))
          )
          (if (> total 0)
            (setq sel_done t)
            (princ "\n[POS] [INFO] В выделении не найдено подходящих текстов. Переход к режиму по слою/всему чертежу.")
          )
        )
        ;; Нет выделения
        (if (= view_mode "viewport")
          ;; В режиме viewport не переходим к полному подсчету
          (progn
            (princ "\n[POS] [INFO] Включен режим по выделению, но выделение отсутствует. Подсчет отменен для режима viewport.")
            (setq sel_done t)
          )
          ;; В режиме By DWG: не считаем весь чертеж — показываем сообщение
          (progn
            (princ "\n[POS] [INFO] Включен режим по выделению, но выделение отсутствует.")
            (alert "Ничего не выделено.\n\nВыделите объекты для подсчёта или переключитесь на режим By layer.")
            (setq sel_done t)
          )
        )
      )
    )
  )
  
  ;; -------------------------------
  ;; ЕСЛИ ВЫДЕЛЕНИЕ НИЧЕГО НЕ ДАЛО → РЕЖИМ \"ПО СЛОЮ\"
  ;; -------------------------------
  (if (not sel_done)
    (progn
      ;; 1) Все TEXT в чертеже
      (setq ss_text (ssget "_X" '((0 . "TEXT"))))
      (if ss_text
        (progn
          (setq total_objects (sslength ss_text))
          (princ (strcat "\n[POS] [INFO] Найдено TEXT объектов в чертеже: " (itoa total_objects)))
          (repeat (setq i total_objects)
            (setq ent (ssname ss_text (setq i (1- i))))
            (setq ent_data (entget ent))
            (setq txt_layer (cdr (assoc 8 ent_data)))
            (setq txt       (cdr (assoc 1 ent_data)))
            (setq res (pos-process-text txt_layer txt layer_name scope result_dict total))
            (setq result_dict (car res)
                  total      (cadr res))
          )
        )
      )
      
      ;; 2) Все MTEXT в чертеже
      (setq ss_mtext (ssget "_X" '((0 . "MTEXT"))))
      (if ss_mtext
        (progn
          (setq total_objects (sslength ss_mtext))
          (princ (strcat "\n[POS] [INFO] Найдено MTEXT объектов в чертеже: " (itoa total_objects)))
          (repeat (setq i total_objects)
            (setq ent (ssname ss_mtext (setq i (1- i))))
            (setq ent_data (entget ent))
            (setq txt_layer (cdr (assoc 8 ent_data)))
            (setq txt       (cdr (assoc 1 ent_data)))
            (setq res (pos-process-text txt_layer txt layer_name scope result_dict total))
            (setq result_dict (car res)
                  total      (cadr res))
          )
        )
      )
      
      ;; 3) Все INSERT в чертеже (для учёта текстов внутри блоков/Xref)
      (setq ss_insert (ssget "_X" '((0 . "INSERT"))))
      (if ss_insert
        (progn
          (setq total_objects (sslength ss_insert))
          (princ (strcat "\n[POS] [INFO] Найдено INSERT объектов в чертеже: " (itoa total_objects)))
          (repeat (setq i total_objects)
            (setq ent (ssname ss_insert (setq i (1- i))))
            (setq ent_data (entget ent))
            (setq blk_name  (cdr (assoc 2 ent_data)))
            (setq blk_layer (cdr (assoc 8 ent_data)))
            (if (= blk_layer "0")
              (setq blk_layer layer_name)
            )
            (setq blk_res (pos-count-texts-in-block
                            blk_name
                            blk_layer
                            layer_name
                            scope
                            result_dict
                            total))
            (setq result_dict (car blk_res)
                  total      (cadr blk_res))
          )
        )
      )
    )
  )
  
  ;; -------------------------------
  ;; ЗАВЕРШЕНИЕ
  ;; -------------------------------
  (if (= total 0)
    (progn
      (princ "\n[POS] [ERROR] Текстов не найдено")
      nil
    )
    (progn
      (setq *pos-total-count* total)
      (if (= scope "all")
        ;; Группировка по слоям: result_dict = ((layer_name ((pos count) ...)) ...)
        (progn
          ;; Сортируем позиции внутри каждого слоя
          (setq result_dict
                (mapcar
                  '(lambda (pair / ln lr)
                     (setq ln (car pair))
                     (setq lr (cadr pair))
                     (list ln (vl-sort lr
                                  '(lambda (a b) (< (car a) (car b))))))
                  result_dict))
          ;; Сортируем слои по имени
          (setq result_dict
                (vl-sort result_dict
                  '(lambda (a b)
                     (< (strcase (car a)) (strcase (car b))))))
          (princ (strcat "\n[POS] Обработано текстов (все слои): " (itoa total)))
          result_dict
        )
        ;; Обычный режим: один слой, словарь позиций
        (progn
          ;; Сортируем по номеру позиции
          (setq result_dict
                (vl-sort result_dict
                  (function (lambda (a b) (< (car a) (car b))))))
          (princ (strcat "\n[POS] Обработано текстов: " (itoa total)))
          (princ (strcat "\n[POS] Уникальных позиций: " (itoa (length result_dict))))
          result_dict
        )
      )
    )
  )
)

;; ===================================================
;; 5. НАСТРОЙКА ДИАЛОГОВОГО ОКНА
;; ===================================================
(defun setup_dialog (/ layers idx default_layer title_text mode scope view_mode)
  ;; Формируем заголовок с версией
  (setq title_text (strcat "POS COUNTER v4.0 | AutoCAD " *acad-version*))
  (set_tile "title" title_text)
  
  ;; Заполняем список слоев
  (setq layers (get_all_layers))
  (setq default_layer (cdr (assoc "default_layer" *pos-settings*)))
  
  (start_list "layer_list")
  (mapcar 'add_list layers)
  (end_list)
  
  ;; Выбираем слой по умолчанию
  (setq idx (vl-position default_layer layers))
  (if (not idx) (setq idx 0))
  (set_tile "layer_list" (itoa idx))
  
  ;; Устанавливаем начальный текст
  (start_list "results_box")
  (add_list (strcat "ДОБРО ПОЖАЛОВАТЬ В POS COUNTER v4.0"))
  (add_list (strcat "Версия AutoCAD: " *acad-version*))
  (add_list "══════════════════════════════════════")
  (add_list "")
  (add_list "ИНСТРУКЦИЯ:")
  (add_list "1. Выберите слой из списка")
  (add_list "2. Нажмите кнопку ПОСЧИТАТЬ")
  (add_list "3. Результаты появятся здесь")
  (add_list "4. Нажмите ЭКСПОРТ для сохранения")
  (add_list "")
  (add_list "Статус: Готов к работе")
  (end_list)
  
  ;; Обновляем статус
  (set_tile "status" "Выберите слой и нажмите ПОСЧИТАТЬ")
  
  ;; Сохраняем текущий слой
  (setq *pos-current-layer* (nth idx layers))

  ;; Устанавливаем режим подсчета
  (setq mode (cdr (assoc "mode" *pos-settings*)))
  (if (or (not mode) (= mode ""))
    (setq mode "layer")
  )
  (if (= mode "selection")
    (progn
      (set_tile "mode_layer" "0")
      (set_tile "mode_selection" "1")
      (set_tile "status" "Режим: по выделению. Выберите слой и нажмите ПОСЧИТАТЬ")
    )
    (progn
      (set_tile "mode_layer" "1")
      (set_tile "mode_selection" "0")
      (set_tile "status" "Режим: по слою. Выберите слой и нажмите ПОСЧИТАТЬ")
    )
  )

  ;; Устанавливаем область подсчета (только слой / все слои)
  (setq scope (cdr (assoc "layer_scope" *pos-settings*)))
  (if (or (not scope) (= scope ""))
    (setq scope "current")
  )
  (if (= scope "all")
    (progn
      (set_tile "scope_current" "0")
      (set_tile "scope_all" "1")
    )
    (progn
      (set_tile "scope_current" "1")
      (set_tile "scope_all" "0")
    )
  )

  ;; Устанавливаем режим по DWG / по viewport
  (setq view_mode (cdr (assoc "view_mode" *pos-settings*)))
  (if (or (not view_mode) (= view_mode ""))
    (setq view_mode "dwg")
  )
  (if (= view_mode "viewport")
    (progn
      (set_tile "view_dwg" "0")
      (set_tile "view_viewport" "1")
      (set_tile "status" "Режим: по viewport. Выберите видовое окно на листе, слой и нажмите ПОСЧИТАТЬ")
    )
    (progn
      (set_tile "view_dwg" "1")
      (set_tile "view_viewport" "0")
      ;; статус уже установлен выше, не меняем
    )
  )
)

;; ===================================================
;; 3. ОБРАБОТКА ВЫБОРА СЛОЯ
;; ===================================================
(defun on_layer_select (index / layers layer_name)
  (setq layers (get_all_layers))
  (setq layer_name (nth (atoi index) layers))
  (setq *pos-current-layer* layer_name)
  (set_tile "status" (strcat " Выбран слой: " layer_name))
  
  ;; Сохраняем в настройках
  (setq *pos-settings* 
    (subst (cons "last_layer" layer_name) 
           (assoc "last_layer" *pos-settings*) 
           *pos-settings*))
)

;; ===================================================
;; 3.1. ОБРАБОТКА ПЕРЕКЛЮЧЕНИЯ РЕЖИМА
;; ===================================================
(defun on_mode_change (/ mode)
  ;; Определяем текущий режим по состоянию радиокнопок
  (if (= (get_tile "mode_selection") "1")
    (setq mode "selection")
    (setq mode "layer")
  )
  ;; Сохраняем режим в настройках
  (setq *pos-settings*
    (subst (cons "mode" mode)
           (assoc "mode" *pos-settings*)
           *pos-settings*))
  ;; Обновляем статус
  (if (= mode "selection")
    (set_tile "status" "Режим: по выделению. Выберите слой и нажмите ПОСЧИТАТЬ")
    (set_tile "status" "Режим: по слою. Выберите слой и нажмите ПОСЧИТАТЬ")
  )
)

;; ===================================================
;; 3.2. ОБРАБОТКА ВЫБОРА ОБЛАСТИ ПОДСЧЕТА (ТОЛЬКО СЛОЙ / ВСЕ СЛОИ)
;; ===================================================
(defun on_scope_change (/ scope)
  ;; Определяем область по состоянию радиокнопок
  (if (= (get_tile "scope_all") "1")
    (setq scope "all")
    (setq scope "current")
  )
  ;; Сохраняем область в настройках
  (setq *pos-settings*
    (subst (cons "layer_scope" scope)
           (assoc "layer_scope" *pos-settings*)
           *pos-settings*))
  ;; Обновляем статус (кратко, без изменения основного текста режима)
  (if (= scope "all")
    (set_tile "status" "Подсчет: все слои")
    (set_tile "status" "Подсчет: только выбранный слой")
  )
)

;; ===================================================
;; 3.3. ОБРАБОТКА ПЕРЕКЛЮЧЕНИЯ РЕЖИМА ПО DWG / ПО VIEWPORT
;; ===================================================
(defun on_viewmode_change (/ view_mode)
  ;; Определяем режим по состоянию радиокнопок
  (if (= (get_tile "view_viewport") "1")
    (setq view_mode "viewport")
    (setq view_mode "dwg")
  )
  ;; Сохраняем режим в настройках
  (setq *pos-settings*
    (subst (cons "view_mode" view_mode)
           (assoc "view_mode" *pos-settings*)
           *pos-settings*))
  ;; Обновляем статус
  (if (= view_mode "viewport")
    (set_tile "status" "Режим: по viewport. Кликните по видовому окну на листе и выберите слой.")
    (set_tile "status" "Режим: по объектам DWG. Выберите слой и нажмите ПОСЧИТАТЬ.")
  )
)

;; ===================================================
;; 3.4. НАВИГАЦИЯ: ПОДСВЕТКА РАСПОЗНАННЫХ ВЫНОСОК НА ЧЕРТЕЖЕ
;; ===================================================
(defun on_show (/ mode scope view_mode res highlight_ents ss)
  (if (not *pos-results*)
    (progn
      (set_tile "status" "[ERROR] Сначала выполните подсчет!")
      (alert "[WARNING] Сначала выполните подсчет позиций, затем используйте навигацию.")
      nil
    )
    (progn
      (setq mode (cdr (assoc "mode" *pos-settings*)))
      (if (or (not mode) (= mode ""))
        (setq mode "layer")
      )
      (setq scope (cdr (assoc "layer_scope" *pos-settings*)))
      (if (or (not scope) (= scope ""))
        (setq scope "current")
      )
      (setq view_mode (cdr (assoc "view_mode" *pos-settings*)))
      (if (or (not view_mode) (= view_mode ""))
        (setq view_mode "dwg")
      )
      ;; Ограничения первой версии: навигация только в режиме By DWG
      (cond
        ((/= view_mode "dwg")
         (set_tile "status" "[INFO] Навигация пока доступна только в режиме By DWG.")
         (alert "[INFO] Навигация по выноскам сейчас работает только в режиме подсчета по всему DWG (By DWG).")
         nil
        )
        (t
         (set_tile "status" "Поиск выносок для подсветки...")
         (setq highlight_ents (pos-build-highlight-selection *pos-results* *pos-current-layer* scope))
         (if (and highlight_ents (> (length highlight_ents) 0))
           (progn
             (setq ss (ssadd))
             (foreach e highlight_ents
               (if (and e (entget e))
                 (setq ss (ssadd e ss))
               )
             )
             (if (and ss (> (sslength ss) 0))
               (progn
                 (sssetfirst nil ss)
                 (set_tile "status" (strcat "[OK] Подсвечено выносок: " (itoa (sslength ss))))
               )
               (progn
                 (set_tile "status" "[INFO] Подходящие выноски не найдены.")
                 (alert "[INFO] Подходящие выноски для подсветки не найдены.")
               )
             )
           )
           (progn
             (set_tile "status" "[INFO] Подходящие выноски не найдены.")
             (alert "[INFO] Подходящие выноски для подсветки не найдены.")
           )
         )
        )
      )
    )
  )
)

;; ===================================================
;; 6. ОТОБРАЖЕНИЕ РЕЗУЛЬТАТОВ В ОКНЕ (ДОЛЖНА БЫТЬ ПЕРЕД on_count)
;; ===================================================
;; Результаты для одного слоя: results = ((pos count) ...)
(defun display_results (results layer_name / lines total_items pos count percent item)
  (setq lines '())
  (setq total_items (apply '+ (mapcar 'cadr results)))
  
  ;; Формируем заголовок
  (setq lines (append lines
    (list 
      (strcat "РЕЗУЛЬТАТЫ ДЛЯ СЛОЯ: " layer_name)
      "══════════════════════════════════════════"
      "Номер позиции   Количество   Слой              Процент"
      "──────────────────────────────────────────────────────"
    )
  ))
  
  ;; Добавляем данные
  (foreach item results
    (setq pos (car item))
    (setq count (cadr item))
    (setq percent (rtos (* 100.0 (/ (float count) total_items)) 2 2))
    
    ;; Форматируем строку: номер позиции, количество, слой, процент
    (cond
      ((< pos 10)
        (setq lines (append lines
          (list (strcat "    " (itoa pos) 
                        "         " 
                        (itoa count) 
                        "    " 
                        (vl-princ-to-string layer_name) 
                        "    " 
                        percent "%"))
        ))
      )
      ((< pos 100)
        (setq lines (append lines
          (list (strcat "   " (itoa pos) 
                        "         " 
                        (itoa count) 
                        "    " 
                        (vl-princ-to-string layer_name) 
                        "    " 
                        percent "%"))
        ))
      )
      ((< pos 1000)
        (setq lines (append lines
          (list (strcat "  " (itoa pos) 
                        "         " 
                        (itoa count) 
                        "    " 
                        (vl-princ-to-string layer_name) 
                        "    " 
                        percent "%"))
        ))
      )
      (t
        (setq lines (append lines
          (list (strcat (itoa pos) 
                        "         " 
                        (itoa count) 
                        "    " 
                        (vl-princ-to-string layer_name) 
                        "    " 
                        percent "%"))
        ))
      )
    )
  )
  
  ;; Добавляем итоговую строку
  (setq lines (append lines
    (list 
      "──────────────────────────────────────────────────────"
      (strcat "ИТОГО:              " 
              (itoa total_items) 
              "    " 
              (vl-princ-to-string layer_name) 
              "    100.00%")
    )
  ))
  
  ;; Отображаем результаты в списке
  (start_list "results_box")
  (mapcar 'add_list lines)
  (end_list)
)

;; Результаты по всем слоям: results = ((layer_name ((pos count) ...)) ...)
(defun display_results_all (results / lines layer_name layer_results total_items pos count percent item)
  (setq lines '())
  (foreach layer_data results
    (setq layer_name    (car layer_data))
    (setq layer_results (cadr layer_data))
    (setq total_items   (apply '+ (mapcar 'cadr layer_results)))
    (if (= total_items 0) (setq total_items 1))
    ;; Заголовок слоя
    (setq lines (append lines
      (list
        (strcat "СЛОЙ: " (vl-princ-to-string layer_name))
        "══════════════════════════════════════════"
        "Номер позиции   Количество   Слой              Процент"
        "──────────────────────────────────────────────────────"
      )
    ))
    ;; Строки позиций
    (foreach item layer_results
      (setq pos   (car item))
      (setq count (cadr item))
      (setq percent (rtos (* 100.0 (/ (float count) total_items)) 2 2))
      (cond
        ((< pos 10)
         (setq lines (append lines
           (list (strcat "    " (itoa pos)
                         "         "
                         (itoa count)
                         "    "
                         (vl-princ-to-string layer_name)
                         "    "
                         percent "%")))))
        ((< pos 100)
         (setq lines (append lines
           (list (strcat "   " (itoa pos)
                         "         "
                         (itoa count)
                         "    "
                         (vl-princ-to-string layer_name)
                         "    "
                         percent "%")))))
        ((< pos 1000)
         (setq lines (append lines
           (list (strcat "  " (itoa pos)
                         "         "
                         (itoa count)
                         "    "
                         (vl-princ-to-string layer_name)
                         "    "
                         percent "%")))))
        (t
         (setq lines (append lines
           (list (strcat (itoa pos)
                         "         "
                         (itoa count)
                         "    "
                         (vl-princ-to-string layer_name)
                         "    "
                         percent "%")))))
      )
    )
    ;; Итог по слою и пустая строка
    (setq lines (append lines
      (list
        "──────────────────────────────────────────────────────"
        (strcat "ИТОГО ПО СЛОЮ '" (vl-princ-to-string layer_name) "': "
                (itoa total_items) "    100.00%")
        ""
      )
    ))
  )
  ;; Отображаем результаты в списке
  (start_list "results_box")
  (mapcar 'add_list lines)
  (end_list)
)

;; Результаты по viewport'ам.
;; При scope=\"current\" results = ((layout handle layer_name ((pos count) ...)) ...)
;; При scope=\"all\"     results = ((layout handle ((layer_name ((pos count) ...)) ...)) ...)
(defun display_results_viewports (results / lines scope viewport_data
                                           layout_name vp_handle
                                           layer_name layer_results layer_list
                                           total_items pos count percent item)
  (setq lines '())
  (setq scope (cdr (assoc "layer_scope" *pos-settings*)))
  (if (or (not scope) (= scope ""))
    (setq scope "current")
  )
  (foreach viewport_data results
    (setq layout_name (nth 0 viewport_data))
    (setq vp_handle   (nth 1 viewport_data))
    ;; Заголовок viewport
    (setq lines (append lines
                  (list
                    (strcat "ЛИСТ: " (vl-princ-to-string layout_name)
                            " | VIEWPORT: " (vl-princ-to-string vp_handle))
                    "══════════════════════════════════════════")))
    (if (= scope "all")
      ;; Структура: (layout handle ((layer_name ((pos count) ...)) ...))
      (progn
        (setq layer_list (nth 2 viewport_data))
        (foreach layer_data layer_list
          (setq layer_name    (car layer_data))
          (setq layer_results (cadr layer_data))
          (setq total_items   (apply '+ (mapcar 'cadr layer_results)))
          (if (= total_items 0) (setq total_items 1))
          ;; Заголовок слоя
          (setq lines (append lines
                        (list
                          (strcat "СЛОЙ: " (vl-princ-to-string layer_name))
                          "Номер позиции   Количество   Слой              Процент"
                          "──────────────────────────────────────────────────────")))
          ;; Строки позиций
          (foreach item layer_results
            (setq pos   (car item))
            (setq count (cadr item))
            (setq percent (rtos (* 100.0 (/ (float count) total_items)) 2 2))
            (setq lines (append lines
                          (list
                            (strcat
                              (rtos pos 2 0) "         "
                              (itoa count) "    "
                              (vl-princ-to-string layer_name) "    "
                              percent "%"))))
          )
          ;; Итог по слою
          (setq lines (append lines
                        (list
                          "──────────────────────────────────────────────────────"
                          (strcat "ИТОГО ПО СЛОЮ '"
                                  (vl-princ-to-string layer_name)
                                  "': "
                                  (itoa total_items)
                                  "    100.00%")
                          "")))
        )
      )
      ;; scope = current, структура: (layout handle layer_name ((pos count) ...))
      (progn
        (setq layer_name    (nth 2 viewport_data))
        (setq layer_results (nth 3 viewport_data))
        (setq total_items   (apply '+ (mapcar 'cadr layer_results)))
        (if (= total_items 0) (setq total_items 1))
        (setq lines (append lines
                      (list
                        (strcat "СЛОЙ: " (vl-princ-to-string layer_name))
                        "Номер позиции   Количество   Слой              Процент"
                        "──────────────────────────────────────────────────────")))
        (foreach item layer_results
          (setq pos   (car item))
          (setq count (cadr item))
          (setq percent (rtos (* 100.0 (/ (float count) total_items)) 2 2))
          (setq lines (append lines
                        (list
                          (strcat
                            (rtos pos 2 0) "         "
                            (itoa count) "    "
                            (vl-princ-to-string layer_name) "    "
                            percent "%"))))
        )
        (setq lines (append lines
                      (list
                        "──────────────────────────────────────────────────────"
                        (strcat "ИТОГО:              "
                                (itoa total_items)
                                "    "
                                (vl-princ-to-string layer_name)
                                "    100.00%")
                        "")))
      )
    )
    ;; Разделитель между viewport'ами
    (setq lines (append lines
                  (list
                    "=================================================="
                    "")))
  )
  ;; Отображаем результаты в списке
  (start_list "results_box")
  (mapcar 'add_list lines)
  (end_list)
)

;; ===================================================
;; 7. ОСНОВНАЯ ФУНКЦИЯ ПОДСЧЕТА
;; ===================================================
(defun on_count (/ layer_name results scope display_layer_name view_mode mode)
  (if (not *pos-current-layer*)
    (progn
      (set_tile "status" "[ERROR] Сначала выберите слой!")
      nil
    )
    (progn
      (setq layer_name *pos-current-layer*)
      ;; Определяем область подсчета для текстовых сообщений
      (setq scope (cdr (assoc "layer_scope" *pos-settings*)))
      (if (or (not scope) (= scope ""))
        (setq scope "current")
      )
      (if (= scope "all")
        (progn
          (setq display_layer_name "ВСЕ СЛОИ")
          (set_tile "status" "Подсчет по всем слоям во всем чертеже/выделении...")
        )
        (progn
          (setq display_layer_name layer_name)
          (set_tile "status" (strcat "Подсчет на слое: " layer_name "..."))
        )
      )
      ;; Определяем режим просмотра (по всему DWG или по viewport)
      ;; Режим просмотра и способ отбора — по текущему состоянию радиокнопок (на момент COUNT)
      (setq view_mode (if (= (get_tile "view_viewport") "1") "viewport" "dwg"))
      (setq mode      (if (= (get_tile "mode_selection") "1") "selection" "layer"))
      (setq *pos-settings* (subst (cons "view_mode" view_mode) (assoc "view_mode" *pos-settings*) *pos-settings*))
      (setq *pos-settings* (subst (cons "mode" mode) (assoc "mode" *pos-settings*) *pos-settings*))
  
      ;; By viewport + By selection: закрываем диалог и запрашиваем выделение в чертеже.
      ;; Если объекты уже выделены (pickfirst) — не показываем alert, код 4.
      ;; Иначе — показываем alert и код 3 для запроса выделения.
      (if (and (equal view_mode "viewport") (equal mode "selection"))
        (progn
          (if (ssget "I")
            (done_dialog 4)
            (progn
              (alert "Режим По viewport + По выделению.\n\nОкно закроется. В командной строке появится запрос — выделите объекты (рамкой или Fence) в активном viewport.\n\nПодсчет выполнится только по выделенным объектам.")
              (done_dialog 3)
            )
          )
        )
        ;; Выполняем подсчет в зависимости от режима
        (if (= view_mode "viewport")
          (progn
            (set_tile "status" "Режим: по viewport. Выберите видовое окно на листе...")
            (setq results (pos-count-in-viewport layer_name))
          )
          (setq results (count_positions layer_name))
        )
      )
  
      (if results
        (progn
          (setq *pos-results* results)
          (if (= view_mode "viewport")
            (progn
              (display_results_viewports results)
              (set_tile "status" "[OK] Подсчет выполнен по выбранному viewport/видам")
            )
            (if (= scope "all")
              (progn
                (display_results_all results)
                (set_tile "status" "[OK] Подсчет выполнен по всем слоям (группировка по слоям)")
              )
              (progn
                (display_results results display_layer_name)
                (set_tile "status" (strcat "[OK] Найдено " 
                                         (itoa (length results)) 
                                         " позиций на слое "
                                         layer_name))
              )
            )
          )
        )
        (progn
          (start_list "results_box")
          (mapcar 'add_list 
            (list 
                "[ERROR] ОШИБКА: ТЕКСТЫ НЕ НАЙДЕНЫ"
              "══════════════════════════════════════"
              ""
              "Возможные причины:"
              "1. На слое нет текстовых объектов"
              "2. Слой пустой или не существует"
              "3. Тексты имеют тип MTEXT"
              "4. Неправильное имя слоя"
              ""
                "Проверьте:"
              (strcat "• Существует ли слой '" layer_name "'")
              "• Есть ли на нем TEXT объекты"
              "• Возможно нужен слой '0'"
            )
          )
          (end_list)
            (set_tile "status" (strcat "[ERROR] На слое '" layer_name "' текстов не найдено"))
          )
        )
      )
    )
)

;; ===================================================
;; 7. ОТОБРАЖЕНИЕ РЕЗУЛЬТАТОВ В ОКНЕ (ДУБЛИКАТ УДАЛЕН, ФУНКЦИЯ ПЕРЕМЕЩЕНА ВЫШЕ)
;; ===================================================

;; ===================================================
;; 8. ЭКСПОРТ В EXCEL (CSV) - УНИВЕРСАЛЬНЫЙ
;; ===================================================
(defun on_export (/ default_filename filename save_path open_file scope)
  (if (not *pos-results*)
    (progn
      (set_tile "status" "[ERROR] Сначала выполните подсчет!")
      (alert "[WARNING] Сначала выполните подсчет позиций!")
      nil
    )
    (progn
      (set_tile "status" "Выбор места сохранения...")
      
      ;; Определяем текущую область подсчёта
      (setq scope (cdr (assoc "layer_scope" *pos-settings*)))
      (if (or (not scope) (= scope ""))
        (setq scope "current")
      )
      ;; Определяем режим просмотра
      (setq view_mode (cdr (assoc "view_mode" *pos-settings*)))
      (if (or (not view_mode) (= view_mode ""))
        (setq view_mode "dwg")
      )
      ;; Создаем имя файла по умолчанию
      (cond
        ;; Режим: по viewport
        ((= view_mode "viewport")
         (setq default_filename (strcat 
           "POS_Viewport_Результаты_"
           (menucmd "M=$(edtime,$(getvar,date),YYYYMMDD_HHMM)")
           "_ACAD"
           (vl-string-translate "." "" *acad-version*)
           ".xlsx"
         ))
        )
        ;; Режим: по всему DWG, все слои
        ((= scope "all")
         (setq default_filename (strcat 
           "POS_Все_слои_"
           (menucmd "M=$(edtime,$(getvar,date),YYYYMMDD_HHMM)")
           "_ACAD"
           (vl-string-translate "." "" *acad-version*)
           ".xlsx"
         ))
        )
        ;; Режим: по всему DWG, один слой
        (t
         (setq default_filename (strcat 
           "POS_Результаты_"
           (vl-string-translate " \\/:*?\"<>|" "__________" *pos-current-layer*)
           "_"
           (menucmd "M=$(edtime,$(getvar,date),YYYYMMDD_HHMM)")
           "_ACAD"
           (vl-string-translate "." "" *acad-version*)
           ".xlsx"
         ))
        )
      )
      
      ;; Показываем диалог выбора файла
      (setq save_path (getfiled "Сохранить результаты в Excel" default_filename "xlsx" 1))
      
      (if save_path
        (progn
          (set_tile "status" "Экспорт в Excel...")
          
          ;; Сохраняем файл в зависимости от режима
          (cond
            ;; Режим viewport: экспорт по видам/листам
            ((= view_mode "viewport")
             (setq filename (export_viewports_to_excel *pos-results* save_path))
            )
            ;; Все слои по всему DWG
            ((= scope "all")
             (setq filename (export_all_layers_to_excel *pos-results* save_path))
            )
            ;; Один слой по всему DWG
            (t
             (setq filename (export_to_excel *pos-results* *pos-current-layer* save_path))
            )
          )
          
          (if filename
            (progn
              (set_tile "status" "[OK] Экспорт завершен")
              
              ;; Сохраняем имя файла для использования после закрытия диалога
              (setq *pos-exported-file* filename)
              (setq *pos-open-file-flag* t)  ;; Флаг для открытия файла
  
  (alert (strcat 
                "[OK] ЭКСПОРТ ВЫПОЛНЕН УСПЕШНО!\n\n"
    "Файл сохранен:\n"
    filename "\n\n"
                "После закрытия окна будет предложено открыть файл."
              ))
              
              filename
            )
            (progn
              (set_tile "status" "[ERROR] Ошибка при экспорте")
              (alert "[ERROR] Не удалось создать файл экспорта!")
              nil
            )
          )
        )
        (progn
          (set_tile "status" "Экспорт отменен")
          nil
        )
      )
    )
  )
)

;; ===================================================
;; 8.6. ВСПОМОГАТЕЛЬНЫЕ ФУНКЦИИ ДЛЯ ЭКСПОРТА (ДУБЛИКАТЫ УДАЛЕНЫ, ФУНКЦИИ ПЕРЕМЕЩЕНЫ ВЫШЕ)
;; ===================================================

(defun export_viewports_to_excel (vp_results save_path / filename excel-app workbook worksheet
                                            row_num err header-range total-range
                                            scope viewport_data layout_name vp_handle
                                            layer_list layer_data layer_name layer_results
                                            total_items pos count percent)
  ;; Загружаем Visual LISP COM поддержку
  (vl-load-com)
  
  ;; Определяем область подсчета
  (setq scope (cdr (assoc "layer_scope" *pos-settings*)))
  (if (or (not scope) (= scope ""))
    (setq scope "current")
  )
  
  ;; Убеждаемся, что расширение .xlsx
  (setq filename save_path)
  (if (not (wcmatch filename "*.xlsx"))
    (setq filename (strcat (vl-string-subst "" (vl-filename-extension filename) filename) ".xlsx"))
  )
  
  (princ (strcat "\n[POS] Создание Excel файла для viewport-режима: " filename))
  
  ;; Пробуем создать Excel файл через COM
  (setq err (vl-catch-all-apply
    (function
      (lambda (/)
        ;; Создаем объект Excel Application
        (setq excel-app (vlax-create-object "Excel.Application"))
        
        ;; Скрываем Excel
        (vlax-put-property excel-app "Visible" :vlax-false)
        (vlax-put-property excel-app "DisplayAlerts" :vlax-false)
        
        ;; Создаем новую книгу
        (setq workbook (vlax-invoke-method excel-app "Workbooks" "Add"))
        (setq worksheet (vlax-get-property workbook "ActiveSheet"))
        
        ;; Устанавливаем название листа
        (vlax-put-property worksheet "Name" "Viewports")
        
        ;; Заголовки
        (vlax-put-property (vlax-get-property worksheet "Cells" "Item" 1 1) "Value2" "Лист")
        (vlax-put-property (vlax-get-property worksheet "Cells" "Item" 1 2) "Value2" "Viewport")
        (vlax-put-property (vlax-get-property worksheet "Cells" "Item" 1 3) "Value2" "Слой")
        (vlax-put-property (vlax-get-property worksheet "Cells" "Item" 1 4) "Value2" "Номер позиции")
        (vlax-put-property (vlax-get-property worksheet "Cells" "Item" 1 5) "Value2" "Количество")
        (vlax-put-property (vlax-get-property worksheet "Cells" "Item" 1 6) "Value2" "Процент")
        
        ;; Форматируем заголовки
        (setq header-range (vlax-get-property worksheet "Range" "A1:F1"))
        (vlax-put-property (vlax-get-property header-range "Font") "Bold" :vlax-true)
        
        (setq row_num 2)
        
        ;; Обходим все viewport-результаты
        (foreach viewport_data vp_results
          (setq layout_name (nth 0 viewport_data))
          (setq vp_handle   (nth 1 viewport_data))
          
          (if (= scope "all")
            ;; Структура: (layout handle ((layer_name ((pos count) ...)) ...))
            (progn
              (setq layer_list (nth 2 viewport_data))
              (foreach layer_data layer_list
                (setq layer_name    (car layer_data))
                (setq layer_results (cadr layer_data))
                (setq total_items   (apply '+ (mapcar 'cadr layer_results)))
                (if (= total_items 0) (setq total_items 1))
                (foreach item layer_results
                  (setq pos   (car item))
                  (setq count (cadr item))
                  (setq percent (rtos (* 100.0 (/ (float count) total_items)) 2 2))
                  ;; Запись строки
                  (vlax-put-property (vlax-get-property worksheet "Cells" "Item" row_num 1) "Value2" (vl-princ-to-string layout_name))
                  (vlax-put-property (vlax-get-property worksheet "Cells" "Item" row_num 2) "Value2" (vl-princ-to-string vp_handle))
                  (vlax-put-property (vlax-get-property worksheet "Cells" "Item" row_num 3) "Value2" (vl-princ-to-string layer_name))
                  (vlax-put-property (vlax-get-property worksheet "Cells" "Item" row_num 4) "Value2" (rtos pos 2 0))
                  (vlax-put-property (vlax-get-property worksheet "Cells" "Item" row_num 5) "Value2" (itoa count))
                  (vlax-put-property (vlax-get-property worksheet "Cells" "Item" row_num 6) "Value2" (strcat percent "%"))
                  (setq row_num (1+ row_num))
                )
                ;; Итог по слою
                (vlax-put-property (vlax-get-property worksheet "Cells" "Item" row_num 1) "Value2" (vl-princ-to-string layout_name))
                (vlax-put-property (vlax-get-property worksheet "Cells" "Item" row_num 2) "Value2" (vl-princ-to-string vp_handle))
                (vlax-put-property (vlax-get-property worksheet "Cells" "Item" row_num 3) "Value2" (strcat "ИТОГО по слою " (vl-princ-to-string layer_name)))
                (vlax-put-property (vlax-get-property worksheet "Cells" "Item" row_num 5) "Value2" (itoa total_items))
                (vlax-put-property (vlax-get-property worksheet "Cells" "Item" row_num 6) "Value2" "100.00%")
                (setq total-range (vlax-get-property worksheet "Range" (strcat "A" (itoa row_num) ":F" (itoa row_num))))
                (vlax-put-property (vlax-get-property total-range "Font") "Bold" :vlax-true)
                (setq row_num (+ row_num 2))
              )
            )
            ;; scope = current, структура: (layout handle layer_name ((pos count) ...))
            (progn
              (setq layer_name    (nth 2 viewport_data))
              (setq layer_results (nth 3 viewport_data))
              (setq total_items   (apply '+ (mapcar 'cadr layer_results)))
              (if (= total_items 0) (setq total_items 1))
              (foreach item layer_results
                (setq pos   (car item))
                (setq count (cadr item))
                (setq percent (rtos (* 100.0 (/ (float count) total_items)) 2 2))
                ;; Запись строки
                (vlax-put-property (vlax-get-property worksheet "Cells" "Item" row_num 1) "Value2" (vl-princ-to-string layout_name))
                (vlax-put-property (vlax-get-property worksheet "Cells" "Item" row_num 2) "Value2" (vl-princ-to-string vp_handle))
                (vlax-put-property (vlax-get-property worksheet "Cells" "Item" row_num 3) "Value2" (vl-princ-to-string layer_name))
                (vlax-put-property (vlax-get-property worksheet "Cells" "Item" row_num 4) "Value2" (rtos pos 2 0))
                (vlax-put-property (vlax-get-property worksheet "Cells" "Item" row_num 5) "Value2" (itoa count))
                (vlax-put-property (vlax-get-property worksheet "Cells" "Item" row_num 6) "Value2" (strcat percent "%"))
                (setq row_num (1+ row_num))
              )
              ;; Итог по viewport/слою
              (vlax-put-property (vlax-get-property worksheet "Cells" "Item" row_num 1) "Value2" (vl-princ-to-string layout_name))
              (vlax-put-property (vlax-get-property worksheet "Cells" "Item" row_num 2) "Value2" (vl-princ-to-string vp_handle))
              (vlax-put-property (vlax-get-property worksheet "Cells" "Item" row_num 3) "Value2" (strcat "ИТОГО по слою " (vl-princ-to-string layer_name)))
              (vlax-put-property (vlax-get-property worksheet "Cells" "Item" row_num 5) "Value2" (itoa total_items))
              (vlax-put-property (vlax-get-property worksheet "Cells" "Item" row_num 6) "Value2" "100.00%")
              (setq total-range (vlax-get-property worksheet "Range" (strcat "A" (itoa row_num) ":F" (itoa row_num))))
              (vlax-put-property (vlax-get-property total-range "Font") "Bold" :vlax-true)
              (setq row_num (+ row_num 2))
            )
          )
        )
        
        ;; Автоматически подбираем ширину столбцов
        (vlax-invoke-method (vlax-get-property worksheet "Columns") "AutoFit")
        
        ;; Сохраняем файл
        (vlax-invoke-method workbook "SaveAs" filename 51)  ;; 51 = xlOpenXMLWorkbook (.xlsx)
        
        ;; Закрываем книгу
        (vlax-invoke-method workbook "Close" :vlax-false)
        
        ;; Закрываем Excel
        (vlax-invoke-method excel-app "Quit")
        
        ;; Освобождаем объекты
        (vlax-release-object worksheet)
        (vlax-release-object workbook)
        (vlax-release-object excel-app)
        
        (princ "\n[POS] [OK] Excel файл для viewport-режима успешно создан")
        t
      )
    )
  ))
  
  ;; Проверяем результат
  (if (vl-catch-all-error-p err)
    (progn
      (princ (strcat "\n[POS] [ERROR] Ошибка при создании Excel файла (viewport): "
                     (vl-catch-all-error-message err)))
      nil
    )
    (progn
      ;; Проверяем, что файл создан успешно
      (if (findfile filename)
        (progn
          (princ (strcat "\n[POS] [OK] Файл viewport-результатов успешно создан: " filename))
          filename
        )
        (progn
          (princ (strcat "\n[POS] [WARNING] Файл viewport-результатов создан, но не найден: " filename))
          nil
        )
      )
    )
  )
)

(defun export_to_excel (results layer_name save_path / filename excel-app workbook worksheet total_items pos count percent item row_num err header-range total-range cell)
  ;; Загружаем Visual LISP COM поддержку
  (vl-load-com)
  
  ;; Используем переданный путь или создаем имя файла по умолчанию
  (if save_path
    (setq filename save_path)
    (progn
      ;; Создаем имя файла по умолчанию
  (setq filename (strcat 
    (getvar "dwgprefix")
    "POS_Результаты_"
    (vl-string-translate " \\/:*?\"<>|" "__________" layer_name)
    "_"
    (menucmd "M=$(edtime,$(getvar,date),YYYYMMDD_HHMM)")
    "_ACAD"
    (vl-string-translate "." "" *acad-version*)
        ".xlsx"
      ))
    )
  )
  
  ;; Убеждаемся, что расширение .xlsx
  (if (not (wcmatch filename "*.xlsx"))
    (setq filename (strcat (vl-string-subst "" (vl-filename-extension filename) filename) ".xlsx"))
  )
  
  (princ (strcat "\n[POS] Создание Excel файла: " filename))
  
  ;; Пробуем создать Excel файл через COM
  (setq err (vl-catch-all-apply
    (function
      (lambda (/)
        ;; Создаем объект Excel Application
        (setq excel-app (vlax-create-object "Excel.Application"))
        
        ;; Скрываем Excel (необязательно, но лучше)
        (vlax-put-property excel-app "Visible" :vlax-false)
        (vlax-put-property excel-app "DisplayAlerts" :vlax-false)
        
        ;; Создаем новую книгу
        (setq workbook (vlax-invoke-method excel-app "Workbooks" "Add"))
        (setq worksheet (vlax-get-property workbook "ActiveSheet"))
        
        ;; Устанавливаем название листа
        (vlax-put-property worksheet "Name" "Результаты подсчета")
        
        ;; Записываем заголовки (используем Value2 для избежания проблем с типами)
        (vlax-put-property (vlax-get-property worksheet "Cells" "Item" 1 1) "Value2" "Номер позиции")
        (vlax-put-property (vlax-get-property worksheet "Cells" "Item" 1 2) "Value2" "Количество")
        (vlax-put-property (vlax-get-property worksheet "Cells" "Item" 1 3) "Value2" "Слой")
        (vlax-put-property (vlax-get-property worksheet "Cells" "Item" 1 4) "Value2" "Процент")
        
        ;; Форматируем заголовки (жирный шрифт)
        (setq header-range (vlax-get-property worksheet "Range" "A1:D1"))
        (vlax-put-property (vlax-get-property header-range "Font") "Bold" :vlax-true)
        
        ;; Рассчитываем общее количество
        (setq total_items (apply '+ (mapcar 'cadr results)))
        
        ;; Проверяем, что total_items не равен 0
        (if (= total_items 0)
          (setq total_items 1)  ;; Избегаем деления на ноль
        )
        
        ;; Записываем данные (используем Value2 и безопасное преобразование типов)
        (setq row_num 2)
        (foreach item results
          (setq pos (car item))
          (setq count (cadr item))
          (setq percent (rtos (* 100.0 (/ (float count) total_items)) 2 2))
          
          ;; Все значения записываем как строки через Value2
          (vlax-put-property (vlax-get-property worksheet "Cells" "Item" row_num 1) "Value2" (itoa pos))
          (vlax-put-property (vlax-get-property worksheet "Cells" "Item" row_num 2) "Value2" (itoa count))
          (vlax-put-property (vlax-get-property worksheet "Cells" "Item" row_num 3) "Value2" (vl-princ-to-string layer_name))
          (vlax-put-property (vlax-get-property worksheet "Cells" "Item" row_num 4) "Value2" (strcat percent "%"))
          
          (setq row_num (1+ row_num))
        )
        
        ;; Записываем итоговую строку (используем Value2)
        (vlax-put-property (vlax-get-property worksheet "Cells" "Item" row_num 1) "Value2" "ИТОГО")
        (vlax-put-property (vlax-get-property worksheet "Cells" "Item" row_num 2) "Value2" (itoa total_items))
        (vlax-put-property (vlax-get-property worksheet "Cells" "Item" row_num 3) "Value2" (vl-princ-to-string layer_name))
        (vlax-put-property (vlax-get-property worksheet "Cells" "Item" row_num 4) "Value2" "100.00%")
        
        ;; Форматируем итоговую строку (жирный шрифт)
        (setq total-range (vlax-get-property worksheet "Range" (strcat "A" (itoa row_num) ":D" (itoa row_num))))
        (vlax-put-property (vlax-get-property total-range "Font") "Bold" :vlax-true)
        
        ;; Автоматически подбираем ширину столбцов
        (vlax-invoke-method (vlax-get-property worksheet "Columns") "AutoFit")
        
        ;; Сохраняем файл
        (vlax-invoke-method workbook "SaveAs" filename 51)  ;; 51 = xlOpenXMLWorkbook (.xlsx)
        
        ;; Закрываем книгу
        (vlax-invoke-method workbook "Close" :vlax-false)
        
        ;; Закрываем Excel
        (vlax-invoke-method excel-app "Quit")
        
        ;; Освобождаем объекты
        (vlax-release-object worksheet)
        (vlax-release-object workbook)
        (vlax-release-object excel-app)
        
        (princ "\n[POS] [OK] Excel файл успешно создан")
        t
      )
    )
  ))
  
  ;; Проверяем результат
  (if (vl-catch-all-error-p err)
    (progn
      (princ (strcat "\n[POS] [ERROR] Ошибка при создании Excel файла: " (vl-catch-all-error-message err)))
      (princ "\n[POS] [INFO] Пробую создать CSV файл как резервный вариант...")
      
      ;; Резервный вариант - создаем CSV файл
      (setq filename (vl-string-subst ".csv" ".xlsx" filename))
      (setq filename (export_to_csv results layer_name filename))
    )
    (progn
      ;; Проверяем, что файл создан успешно
      (if (findfile filename)
        (progn
          (princ (strcat "\n[POS] [OK] Файл успешно создан: " filename))
        )
        (progn
          (princ (strcat "\n[POS] [WARNING] Файл создан, но не найден: " filename))
        )
      )
    )
  )
  
  ;; Возвращаем имя файла только если файл был успешно создан
  (if (findfile filename)
    filename
    nil
  )
)

;; Резервная функция для создания CSV файла (дубликат удален, функция перемещена выше)
(defun export_to_csv (results layer_name filename / f total_items pos count percent item)
  (princ (strcat "\n[POS] Создание CSV файла: " filename))
  
  (setq f (open filename "w"))
  
  (if (not f)
    (progn
      (princ (strcat "\n[POS] [ERROR] Не удалось создать файл: " filename))
      nil
    )
    (progn
      ;; UTF-8 BOM для новых версий
      (if (>= *acad-major-version* 23)
        (progn
      (write-char 239 f)
      (write-char 187 f)
      (write-char 191 f)
    )
  )
  
  ;; Заголовок
  (write-line "Номер позиции;Количество;Слой;Процент" f)
  
  ;; Рассчитываем общее количество
      (setq total_items (apply '+ (mapcar 'cadr results)))
      (if (= total_items 0) (setq total_items 1))
  
  ;; Данные
  (foreach item results
    (setq pos (car item))
    (setq count (cadr item))
    (setq percent (rtos (* 100.0 (/ (float count) total_items)) 2 2))
    
    (write-line (strcat 
      (itoa pos) ";"
      (itoa count) ";"
      layer_name ";"
      percent "%"
    ) f)
  )
  
  ;; Итоговая строка
  (write-line (strcat 
    "ИТОГО;" 
    (itoa total_items) ";"
    layer_name ";"
    "100.00%"
  ) f)
  
  (close f)
      (princ "\n[POS] [OK] CSV файл успешно создан")
  filename
)
  )
)

;; ===================================================
;; ЗАГРУЗКА И ИНИЦИАЛИЗАЦИЯ
;; ===================================================
(princ " [OK] УСПЕШНО!")
(princ "\n============================================")
(princ "\n     POS COUNTER v4.0 ЗАГРУЖЕН")
(if (and *acad-version* (> (strlen *acad-version*) 0))
  (princ (strcat "\n    для AutoCAD " *acad-version*))
  (princ "\n    для AutoCAD")
)
(princ "\n============================================")
(princ "\nОСНОВНАЯ КОМАНДА: POSC")
(princ "\n")
(princ "\nБЫСТРЫЙ СТАРТ:")
(princ "\n   1. Введите POSC для открытия окна")
(princ "\n   2. Выберите слой из списка")
(princ "\n   3. Нажмите кнопку ПОСЧИТАТЬ")
(princ "\n   4. Нажмите ЭКСПОРТ для сохранения")
(princ "\n")
(princ)