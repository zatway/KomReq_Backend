import type { UserDto } from './types/interfaces/userDto';
import type { ChangePasswordModel } from './types/interfaces/changePasswordModel';
import type { ChangeRoleModel } from './types/interfaces/changeRoleModel';
import { createApi } from './http';

export const changeRole = async (data: ChangeRoleModel): Promise<{ message: string }> => {
    const response = await api.post<{ message: string }>(`/change-role`, data);
    return response.data;
};

// Поиск пользователей по роли (Admin, Manager)
export const searchUsers = async (query?: string, role?: string): Promise<UserDto[]> => {
    const response = await api.get<UserDto[]>(`/search-users`, { params: { query, role } });
    return response.data;
};

