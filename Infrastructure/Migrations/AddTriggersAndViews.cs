using Microsoft.EntityFrameworkCore.Migrations;

namespace Infrastructure.Migrations
{
    public partial class AddTriggersAndViews : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- Создание перечислений (если еще не созданы)
DO $$ 
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'request_priority') THEN
        CREATE TYPE public.request_priority AS ENUM ('Low', 'Medium', 'High', 'Urgent');
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'request_assignment_role') THEN
        CREATE TYPE public.request_assignment_role AS ENUM ('Manager', 'Technician');
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'notification_type') THEN
        CREATE TYPE public.notification_type AS ENUM ('StatusChange', 'Assignment', 'FileAdded');
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'notification_delivery_status') THEN
        CREATE TYPE public.notification_delivery_status AS ENUM ('Pending', 'Sent', 'Failed');
    END IF;
END $$;

-- Функция для логирования в public.""AuditLogs""
CREATE OR REPLACE FUNCTION public.""LogAuditAction""()
RETURNS TRIGGER AS $$
BEGIN
    INSERT INTO public.""AuditLogs"" (""UserId"", ""Action"", ""EntityId"", ""EntityType"", ""Details"", ""IpAddress"", ""Timestamp"")
    VALUES (
        COALESCE(NEW.""ChangedByUserId"", NEW.""UploadedByUserId"", NEW.""UserId"", OLD.""ChangedByUserId"", OLD.""UploadedByUserId"", OLD.""UserId"", '1'),
        TG_OP || '_' || TG_TABLE_NAME,
        COALESCE(NEW.""Id"", OLD.""Id""),
        TG_TABLE_NAME,
        jsonb_build_object(
            'old', row_to_json(OLD),
            'new', row_to_json(NEW)
        ),
        inet_client_addr(),
        CURRENT_TIMESTAMP
    );
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Триггер для логирования изменений в public.""Requests""
DROP TRIGGER IF EXISTS audit_requests_trigger ON public.""Requests"";
CREATE TRIGGER audit_requests_trigger
AFTER INSERT OR UPDATE OR DELETE ON public.""Requests""
FOR EACH ROW EXECUTE FUNCTION public.""LogAuditAction""();

-- Триггер для логирования изменений в public.""RequestAssignments""
DROP TRIGGER IF EXISTS audit_request_assignments_trigger ON public.""RequestAssignments"";
CREATE TRIGGER audit_request_assignments_trigger
AFTER INSERT OR UPDATE OR DELETE ON public.""RequestAssignments""
FOR EACH ROW EXECUTE FUNCTION public.""LogAuditAction""();

-- Триггер для логирования изменений в public.""RequestFiles""
DROP TRIGGER IF EXISTS audit_request_files_trigger ON public.""RequestFiles"";
CREATE TRIGGER audit_request_files_trigger
AFTER INSERT OR UPDATE OR DELETE ON public.""RequestFiles""
FOR EACH ROW EXECUTE FUNCTION public.""LogAuditAction""();

-- Функция для записи в public.""RequestHistories"" и создания уведомлений в public.""Notifications""
CREATE OR REPLACE FUNCTION public.""LogRequestStatusChange""()
RETURNS TRIGGER AS $$
BEGIN
    IF OLD.""CurrentStatusId"" IS DISTINCT FROM NEW.""CurrentStatusId"" THEN
        INSERT INTO public.""RequestHistories"" (
            ""RequestId"",
            ""OldStatusId"",
            ""NewStatusId"",
            ""ChangedByUserId"",
            ""ChangeDate"",
            ""Comment"",
            ""FieldChanged""
        )
        VALUES (
            NEW.""Id"",
            OLD.""CurrentStatusId"",
            NEW.""CurrentStatusId"",
            COALESCE(NEW.""ManagerId"", '1'),
            CURRENT_TIMESTAMP,
            'Статус изменен с ' || (SELECT ""Name"" FROM public.""RequestStatuses"" WHERE ""Id"" = OLD.""CurrentStatusId"") ||
            ' на ' || (SELECT ""Name"" FROM public.""RequestStatuses"" WHERE ""Id"" = NEW.""CurrentStatusId""),
            'CurrentStatusId'
        );

        -- Создание уведомления для клиента
        INSERT INTO public.""Notifications"" (
            ""RequestId"",
            ""ClientId"",
            ""Type"",
            ""Message"",
            ""SentDate"",
            ""IsRead"",
            ""DeliveryStatus""
        )
        SELECT
            NEW.""Id"",
            NEW.""ClientId"",
            'StatusChange',
            'Заявка #' || NEW.""Id"" || ': статус изменен на ' || (SELECT ""Name"" FROM public.""RequestStatuses"" WHERE ""Id"" = NEW.""CurrentStatusId""),
            CURRENT_TIMESTAMP,
            FALSE,
            'Pending';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Триггер для записи в public.""RequestHistories"" при изменении статуса
DROP TRIGGER IF EXISTS request_status_change_trigger ON public.""Requests"";
CREATE TRIGGER request_status_change_trigger
AFTER UPDATE ON public.""Requests""
FOR EACH ROW
WHEN (OLD.""CurrentStatusId"" IS DISTINCT FROM NEW.""CurrentStatusId"")
EXECUTE FUNCTION public.""LogRequestStatusChange""();

-- Функция для уведомления при добавлении файла
CREATE OR REPLACE FUNCTION public.""NotifyFileAdded""()
RETURNS TRIGGER AS $$
BEGIN
    INSERT INTO public.""Notifications"" (
        ""RequestId"",
        ""ClientId"",
        ""Type"",
        ""Message"",
        ""SentDate"",
        ""IsRead"",
        ""DeliveryStatus""
    )
    SELECT
        NEW.""RequestId"",
        r.""ClientId"",
        'FileAdded',
        'К заявке #' || NEW.""RequestId"" || ' прикреплен файл: ' || NEW.""FileName"",
        CURRENT_TIMESTAMP,
        FALSE,
        'Pending'
    FROM public.""Requests"" r
    WHERE r.""Id"" = NEW.""RequestId"";

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Триггер для уведомления при добавлении файла
DROP TRIGGER IF EXISTS request_file_added_trigger ON public.""RequestFiles"";
CREATE TRIGGER request_file_added_trigger
AFTER INSERT ON public.""RequestFiles""
FOR EACH ROW EXECUTE FUNCTION public.""NotifyFileAdded""();

-- Функция для обновления статистики статусов
CREATE OR REPLACE FUNCTION public.""UpdateStatusStatistics""()
RETURNS TRIGGER AS $$
BEGIN
    INSERT INTO public.""StatusStatistics"" (""StatusId"", ""Date"", ""CountRequests"", ""AvgCompletionDays"")
    SELECT
        NEW.""CurrentStatusId"",
        CURRENT_DATE,
        COUNT(*) FILTER (WHERE r.""CurrentStatusId"" = NEW.""CurrentStatusId""),
        AVG(EXTRACT(EPOCH FROM (CURRENT_TIMESTAMP - r.""CreatedDate"")) / 86400.0) FILTER (WHERE r.""CurrentStatusId"" = NEW.""CurrentStatusId"" AND r.""IsActive"" = FALSE)
    FROM public.""Requests"" r
    WHERE r.""CurrentStatusId"" = NEW.""CurrentStatusId""
    ON CONFLICT (""StatusId"", ""Date"")
    DO UPDATE SET
        ""CountRequests"" = EXCLUDED.""CountRequests"",
        ""AvgCompletionDays"" = EXCLUDED.""AvgCompletionDays"";
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Триггер для обновления статистики при изменении статуса
DROP TRIGGER IF EXISTS status_statistics_trigger ON public.""Requests"";
CREATE TRIGGER status_statistics_trigger
AFTER INSERT OR UPDATE OF ""CurrentStatusId"" ON public.""Requests""
FOR EACH ROW EXECUTE FUNCTION public.""UpdateStatusStatistics""();

-- Представление для дашборда заявок
DROP VIEW IF EXISTS public.v_request_dashboard;
CREATE OR REPLACE VIEW public.v_request_dashboard AS
SELECT
    r.""Id"",
    r.""CreatedDate"",
    r.""Priority"",
    r.""Quantity"",
    c.""FullName"" AS client_name,
    e.""Name"" AS equipment_name,
    s.""Name"" AS status_name,
    COUNT(rh.""Id"") AS changes_count
FROM public.""Requests"" r
JOIN public.""Clients"" c ON r.""ClientId"" = c.""Id""
JOIN public.""EquipmentTypes"" e ON r.""EquipmentTypeId"" = e.""Id""
JOIN public.""RequestStatuses"" s ON r.""CurrentStatusId"" = s.""Id""
LEFT JOIN public.""RequestHistories"" rh ON r.""Id"" = rh.""RequestId""
GROUP BY r.""Id"", c.""FullName"", e.""Name"", s.""Name"";

-- Представление для загрузки сотрудников
DROP VIEW IF EXISTS public.v_user_load;
CREATE OR REPLACE VIEW public.v_user_load AS
SELECT
    u.""Id"",
    u.""FullName"",
    u.""Email"",
    COUNT(ra.""Id"") AS active_assignments,
    COALESCE(up.""Workload"", 0) AS workload_hours
FROM public.""AspNetUsers"" u
LEFT JOIN public.""RequestAssignments"" ra ON u.""Id"" = ra.""UserId"" AND ra.""CompletedDate"" IS NULL
LEFT JOIN public.""UserProfiles"" up ON u.""Id"" = up.""UserId""
GROUP BY u.""Id"", u.""FullName"", u.""Email"", up.""Workload"";

-- Комментарии к функциям и представлениям
COMMENT ON FUNCTION public.""LogAuditAction"" IS 'Логирует CRUD-операции в public.""AuditLogs"" для отслеживания действий';
COMMENT ON FUNCTION public.""LogRequestStatusChange"" IS 'Записывает изменения статуса заявки в public.""RequestHistories"" и создает уведомления в public.""Notifications""';
COMMENT ON FUNCTION public.""NotifyFileAdded"" IS 'Создает уведомление в public.""Notifications"" при добавлении файла в public.""RequestFiles""';
COMMENT ON FUNCTION public.""UpdateStatusStatistics"" IS 'Обновляет статистику в public.""StatusStatistics"" при изменении статуса заявки';
COMMENT ON VIEW public.v_request_dashboard IS 'Дашборд заявок с информацией о клиенте, оборудовании, статусе и количестве изменений';
COMMENT ON VIEW public.v_user_load IS 'Загрузка сотрудников (активные назначения и часы работы)';
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- Удаление представлений
DROP VIEW IF EXISTS public.v_request_dashboard;
DROP VIEW IF EXISTS public.v_user_load;

-- Удаление триггеров
DROP TRIGGER IF EXISTS audit_requests_trigger ON public.""Requests"";
DROP TRIGGER IF EXISTS audit_request_assignments_trigger ON public.""RequestAssignments"";
DROP TRIGGER IF EXISTS audit_request_files_trigger ON public.""RequestFiles"";
DROP TRIGGER IF EXISTS request_status_change_trigger ON public.""Requests"";
DROP TRIGGER IF EXISTS request_file_added_trigger ON public.""RequestFiles"";
DROP TRIGGER IF EXISTS status_statistics_trigger ON public.""Requests"";

-- Удаление функций
DROP FUNCTION IF EXISTS public.""LogAuditAction""();
DROP FUNCTION IF EXISTS public.""LogRequestStatusChange""();
DROP FUNCTION IF EXISTS public.""NotifyFileAdded""();
DROP FUNCTION IF EXISTS public.""UpdateStatusStatistics""();

-- Удаление перечислений
DROP TYPE IF EXISTS public.request_priority;
DROP TYPE IF EXISTS public.request_assignment_role;
DROP TYPE IF EXISTS public.notification_type;
DROP TYPE IF EXISTS public.notification_delivery_status;
");
        }
    }
}