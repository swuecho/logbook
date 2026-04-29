<template>
        <div class="app-page app-page--shell lb-admin admin-dashboard">
                <div class="app-shell">
                        <AppTopBar title="Admin">
                                <template #actions-before>
                                        <div class="admin-dashboard__meta">{{ users.length }} users</div>
                                </template>
                        </AppTopBar>
                <el-row :gutter="12" class="stats-row">
                        <el-col :xs="12" :sm="12" :md="6">
                                <el-card shadow="never" class="stats-card">
                                        <div class="stats-icon users-icon">
                                                <Icon icon="mdi:account" width="2.25rem" height="2.25rem" />
                                        </div>
                                        <div class="stats-info">
                                                <div class="stats-title">Total Users</div>
                                                <div class="stats-value">{{ users.length }}</div>
                                        </div>
                                </el-card>
                        </el-col>
                        <el-col :xs="12" :sm="12" :md="6">
                                <el-card shadow="never" class="stats-card">
                                        <div class="stats-icon active-icon">
                                                <Icon icon="mdi:clock-outline" width="2.25rem" height="2.25rem" />
                                        </div>
                                        <div class="stats-info">
                                                <div class="stats-title">Active Today</div>
                                                <div class="stats-value">{{ activeTodayCount }}</div>
                                        </div>
                                </el-card>
                        </el-col>
                        <el-col :xs="12" :sm="12" :md="6">
                                <el-card shadow="never" class="stats-card">
                                        <div class="stats-icon diary-icon">
                                                <Icon icon="mdi:notebook" width="2.25rem" height="2.25rem" />
                                        </div>
                                        <div class="stats-info">
                                                <div class="stats-title">Total Entries</div>
                                                <div class="stats-value">{{ totalEntries }}</div>
                                        </div>
                                </el-card>
                        </el-col>
                        <el-col :xs="12" :sm="12" :md="6">
                                <el-card shadow="never" class="stats-card">
                                        <div class="stats-icon avg-icon">
                                                <Icon icon="mdi:chart-box-outline" width="2.25rem" height="2.25rem" />
                                        </div>
                                        <div class="stats-info">
                                                <div class="stats-title">Avg. Entries/User</div>
                                                <div class="stats-value">{{ averageEntriesPerUser }}</div>
                                        </div>
                                </el-card>
                        </el-col>
                </el-row>

                <el-card shadow="never" class="box-card admin-users-card">
                        <template #header>
                                <div class="header-actions">
                                        <el-input v-model="searchQuery" placeholder="Search users..." clearable class="search-input"
                                                @clear="handleSearch" @input="handleSearch">
                                                <template #prefix>
                                                        <Icon icon="mdi:magnify" width="1.1rem" height="1.1rem" />
                                                </template>
                                        </el-input>

                                        <el-select v-model="statusFilter" placeholder="Status" clearable class="filter-control"
                                                @change="handleFilter">
                                                <el-option label="Active" value="active" />
                                                <el-option label="Recent" value="recent" />
                                                <el-option label="Inactive" value="inactive" />
                                        </el-select>

                                        <el-select v-model="sortBy" placeholder="Sort by" class="filter-control" @change="handleSort">
                                                <el-option label="Newest" value="newest" />
                                                <el-option label="Most Active" value="mostActive" />
                                                <el-option label="Most Entries" value="mostEntries" />
                                        </el-select>
                                </div>
                        </template>

                        <el-table :data="filteredUsers" class="admin-users-table" style="width: 100%" border stripe v-loading="loading"
                                element-loading-text="Loading users...">
                                <el-table-column prop="email" label="Email" min-width="220">
                                        <template #default="scope">
                                                <div class="user-email">
                                                        <span>{{ scope.row.email }}</span>
                                                        <el-button type="primary" link size="small"
                                                                @click="viewUserDetails(scope.row)">
                                                                <Icon icon="mdi:eye" width="1rem" height="1rem" style="margin-right: 4px; vertical-align: middle;" />
                                                                View Details
                                                        </el-button>
                                                </div>
                                        </template>
                                </el-table-column>

                                <el-table-column label="Diary Count" width="120">
                                        <template #default="scope">
                                                <el-tag type="success">
                                                        {{ scope.row.diaryCount }} entries
                                                </el-tag>
                                        </template>
                                </el-table-column>

                                <el-table-column label="Joined Date" width="180">
                                        <template #default="scope">
                                                {{ formatDate(scope.row.dateJoined) }}
                                        </template>
                                </el-table-column>

                                <el-table-column label="Last Login" width="180">
                                        <template #default="scope">
                                                {{ formatDate(scope.row.lastLogin) }}
                                        </template>
                                </el-table-column>

                                <el-table-column label="Status" width="100">
                                        <template #default="scope">
                                                <el-tag :type="getUserStatusType(scope.row)">
                                                        {{ getUserStatus(scope.row) }}
                                                </el-tag>
                                        </template>
                                </el-table-column>

                                <el-table-column label="Actions" width="150">
                                        <template #default="scope">
                                                <el-button size="small" type="danger"
                                                        @click="handleDelete(scope.row)"
                                                        :disabled="!canDeleteUser(scope.row)">
                                                        <Icon icon="mdi:delete" width="1rem" height="1rem" style="margin-right: 4px; vertical-align: middle;" />
                                                        Deactivate
                                                </el-button>
                                        </template>
                                </el-table-column>
                        </el-table>

                        <div class="mobile-user-list" v-loading="loading" element-loading-text="Loading users...">
                                <template v-if="filteredUsers.length">
                                        <article v-for="user in filteredUsers" :key="user.id" class="mobile-user-card">
                                                <div class="mobile-user-card__header">
                                                        <div class="mobile-user-card__email">{{ user.email }}</div>
                                                        <el-tag :type="getUserStatusType(user)">
                                                                {{ getUserStatus(user) }}
                                                        </el-tag>
                                                </div>

                                                <div class="mobile-user-card__details">
                                                        <div>
                                                                <span>Entries</span>
                                                                <strong>{{ user.diaryCount }}</strong>
                                                        </div>
                                                        <div>
                                                                <span>Joined</span>
                                                                <strong>{{ formatDate(user.dateJoined) }}</strong>
                                                        </div>
                                                        <div>
                                                                <span>Last login</span>
                                                                <strong>{{ formatDate(user.lastLogin) }}</strong>
                                                        </div>
                                                </div>

                                                <div class="mobile-user-card__actions">
                                                        <el-button type="primary" link size="small" @click="viewUserDetails(user)">
                                                                <Icon icon="mdi:eye" width="1rem" height="1rem" style="margin-right: 4px; vertical-align: middle;" />
                                                                View Details
                                                        </el-button>
                                                        <el-button size="small" type="danger" @click="handleDelete(user)"
                                                                :disabled="!canDeleteUser(user)">
                                                                <Icon icon="mdi:delete" width="1rem" height="1rem" style="margin-right: 4px; vertical-align: middle;" />
                                                                Deactivate
                                                        </el-button>
                                                </div>
                                        </article>
                                </template>
                                <el-empty v-else-if="!loading" description="No users found" :image-size="72" />
                        </div>

                        <div class="pagination-container pagination-container--desktop">
                                <el-pagination @size-change="handleSizeChange" @current-change="handleCurrentChange"
                                        :current-page="currentPage" :page-sizes="[10, 20, 50, 100]"
                                        :page-size="pageSize" layout="total, sizes, prev, pager, next, jumper"
                                        :total="totalUsers" />
                        </div>
                        <div class="pagination-container pagination-container--mobile">
                                <el-pagination @current-change="handleCurrentChange" :current-page="currentPage"
                                        :page-size="pageSize" layout="prev, pager, next" :pager-count="5"
                                        :total="totalUsers" small />
                        </div>
                </el-card>

                <el-dialog v-model="dialogVisible" title="User Details" width="50%" class="user-details-dialog">
                        <div v-if="selectedUser" class="user-details">
                                <el-descriptions :column="detailsColumnCount" border>
                                        <el-descriptions-item label="Email">{{ selectedUser.email
                                                }}</el-descriptions-item>
                                        <el-descriptions-item label="Status">
                                                <el-tag :type="getUserStatusType(selectedUser)">{{
                                                        getUserStatus(selectedUser)
                                                        }}</el-tag>
                                        </el-descriptions-item>
                                        <el-descriptions-item label="Joined Date">{{ formatDate(selectedUser.dateJoined)
                                                }}</el-descriptions-item>
                                        <el-descriptions-item label="Last Login">{{ formatDate(selectedUser.lastLogin)
                                                }}</el-descriptions-item>
                                        <el-descriptions-item label="Total Entries">{{ selectedUser.diaryCount
                                                }}</el-descriptions-item>
                                        <el-descriptions-item label="Average Entries/Week">
                                                {{ calculateWeeklyAverage(selectedUser) }}
                                        </el-descriptions-item>
                                </el-descriptions>
                        </div>
                </el-dialog>
                </div>
        </div>
</template>

<script>
import { format, isToday, differenceInDays, differenceInWeeks } from 'date-fns'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Icon } from '@iconify/vue'
import AppTopBar from '@/components/AppTopBar.vue'
import { fetchUsersWithDiary, deleteUser } from '@/services/users';
import { getApiErrorMessage } from '@/services/apiError';

export default {
        name: 'AdminDashboard',
        components: { Icon, AppTopBar },
        data() {
                return {
                        users: [],
                        loading: false,
                        searchQuery: '',
                        statusFilter: '',
                        sortBy: 'newest',
                        currentPage: 1,
                        pageSize: 10,
                        dialogVisible: false,
                        selectedUser: null,
                        isNarrowScreen: false
                }
        },
        computed: {
                activeTodayCount() {
                        return this.users.filter(user =>
                                user.lastLogin && isToday(new Date(user.lastLogin))
                        ).length
                },
                totalEntries() {
                        return this.users.reduce((sum, user) => sum + user.diaryCount, 0)
                },
                averageEntriesPerUser() {
                        if (this.users.length === 0) return 0
                        return (this.totalEntries / this.users.length).toFixed(1)
                },
                filteredUsersRaw() {
                        let result = [...this.users]

                        if (this.searchQuery) {
                                const query = this.searchQuery.toLowerCase()
                                result = result.filter(user =>
                                        user.email.toLowerCase().includes(query)
                                )
                        }

                        if (this.statusFilter) {
                                result = result.filter(user =>
                                        this.getUserStatus(user).toLowerCase() === this.statusFilter
                                )
                        }

                        switch (this.sortBy) {
                                case 'newest':
                                        result.sort((a, b) => new Date(b.dateJoined) - new Date(a.dateJoined))
                                        break
                                case 'mostActive':
                                        result.sort((a, b) => new Date(b.lastLogin || 0) - new Date(a.lastLogin || 0))
                                        break
                                case 'mostEntries':
                                        result.sort((a, b) => b.diaryCount - a.diaryCount)
                                        break
                        }
                        return result
                },
                filteredUsers() {
                        const start = (this.currentPage - 1) * this.pageSize
                        return this.filteredUsersRaw.slice(start, start + this.pageSize)
                },
                totalUsers() {
                        return this.filteredUsersRaw.length
                },
                detailsColumnCount() {
                        return this.isNarrowScreen ? 1 : 2
                },
        },
        methods: {
                updateViewportState() {
                        this.isNarrowScreen = window.matchMedia('(max-width: 768px)').matches
                },
                formatDate(date) {
                        if (!date) return 'Never'
                        return format(new Date(date), 'MMM d, yyyy HH:mm')
                },
                getUserStatus(user) {
                        if (!user.lastLogin) return 'Inactive'
                        const daysSinceLastLogin = differenceInDays(new Date(), new Date(user.lastLogin))
                        if (daysSinceLastLogin === 0) return 'Active'
                        if (daysSinceLastLogin <= 7) return 'Recent'
                        return 'Inactive'
                },
                getUserStatusType(user) {
                        const status = this.getUserStatus(user)
                        switch (status) {
                                case 'Active': return 'success'
                                case 'Recent': return 'warning'
                                default: return 'info'
                        }
                },
                calculateWeeklyAverage(user) {
                        if (!user.dateJoined) return 0
                        const weeks = differenceInWeeks(new Date(), new Date(user.dateJoined)) || 1
                        return (user.diaryCount / weeks).toFixed(1)
                },
                handleSearch() {
                        this.currentPage = 1
                },
                handleFilter() {
                        this.currentPage = 1
                },
                handleSort() {
                        this.currentPage = 1
                },
                handleSizeChange(val) {
                        this.pageSize = val
                },
                handleCurrentChange(val) {
                        this.currentPage = val
                },
                viewUserDetails(user) {
                        this.selectedUser = user
                        this.dialogVisible = true
                },
                canDeleteUser(user) {
                        return Boolean(user && user.id)
                },
                async handleDelete(user) {
                        try {
                                await ElMessageBox.confirm(`Deactivate ${user.email}? Their diary entries will be kept.`, 'Warning', {
                                        confirmButtonText: 'Deactivate',
                                        cancelButtonText: 'Cancel',
                                        type: 'warning'
                                })

                                await deleteUser(user.id)
                                ElMessage.success('User deactivated successfully')
                                this.fetchUsers()
                        } catch (error) {
                                if (error !== 'cancel') {
                                        ElMessage.error(getApiErrorMessage(error, 'Failed to delete user'))
                                        console.error('Error deleting user:', error)
                                }
                        }
                },
                async fetchUsers() {
                        this.loading = true
                        try {
                                this.users = await fetchUsersWithDiary()
                        } catch (error) {
                                ElMessage.error(getApiErrorMessage(error, 'Failed to fetch users'))
                                console.error('Error fetching users:', error)
                        } finally {
                                this.loading = false
                        }
                }
        },
        mounted() {
                this.updateViewportState()
                window.addEventListener('resize', this.updateViewportState)
                this.fetchUsers()
        },
        beforeUnmount() {
                window.removeEventListener('resize', this.updateViewportState)
        }
}
</script>

<style scoped>
.admin-dashboard__title {
        font-size: 1rem;
        font-weight: 600;
        color: var(--lb-text);
}

.admin-dashboard__meta {
        color: var(--lb-text-muted);
        font-size: 0.9rem;
}

.stats-row {
        margin-top: 1.25rem;
        margin-bottom: 1.25rem;
        margin-left: auto;
        margin-right: auto;
        display: flex;
        justify-content: flex-start;
}

.stats-card {
        height: 100%;
}

.stats-card :deep(.el-card__body) {
        display: flex;
        align-items: center;
        width: 100%;
        height: 100%;
        padding: 1rem;
}

.stats-icon {
        margin-right: 1rem;
        padding: 0.65rem;
        border-radius: var(--lb-radius-md, 8px);
        display: flex;
        align-items: center;
        justify-content: center;
        color: var(--lb-text-muted, #5a6d7e);
}

.stats-info {
        flex: 1;
}

.stats-title {
        font-size: 0.8rem;
        margin-bottom: 0.35rem;
        color: var(--lb-text-subtle);
}

.stats-value {
        font-size: 1.35rem;
        font-weight: 600;
        font-variant-numeric: tabular-nums;
}

.header-actions {
        display: flex;
        gap: 0.65rem;
        align-items: center;
        padding: 1rem;
        flex-wrap: wrap;
}

.search-input {
        width: 300px;
        max-width: 100%;
}

.filter-control {
        width: 150px;
}

.admin-users-card :deep(.el-card__body) {
        padding: 0;
}

.admin-users-table {
        display: block;
}

.user-email {
        display: flex;
        justify-content: space-between;
        align-items: center;
}

.mobile-user-list {
        display: none;
}

.mobile-user-card {
        padding: 0.9rem 1rem;
        border-bottom: 1px solid var(--lb-border);
        background: #fff;
}

.mobile-user-card:last-child {
        border-bottom: none;
}

.mobile-user-card__header {
        display: flex;
        align-items: flex-start;
        justify-content: space-between;
        gap: 0.75rem;
        margin-bottom: 0.75rem;
}

.mobile-user-card__email {
        min-width: 0;
        overflow-wrap: anywhere;
        font-weight: 600;
        line-height: 1.4;
}

.mobile-user-card__details {
        display: grid;
        gap: 0.55rem;
        margin-bottom: 0.75rem;
}

.mobile-user-card__details div {
        display: flex;
        justify-content: space-between;
        gap: 1rem;
        color: var(--lb-text-muted);
        font-size: 0.85rem;
}

.mobile-user-card__details strong {
        color: var(--lb-text);
        font-weight: 500;
        text-align: right;
}

.mobile-user-card__actions {
        display: flex;
        justify-content: flex-end;
        gap: 0.5rem;
        flex-wrap: wrap;
}

.pagination-container {
        margin-top: 1rem;
        display: flex;
        justify-content: flex-end;
        padding: 0 1rem 1rem;
}

.pagination-container--mobile {
        display: none;
}

.user-details {
        padding: 0;
}

@media (max-width: 992px) {
        .stats-row :deep(.el-col) {
                max-width: 50%;
                flex: 0 0 50%;
                margin-bottom: 1rem;
        }
}

@media (max-width: 768px) {
        .stats-row :deep(.el-col) {
                max-width: 50%;
                flex: 0 0 50%;
        }

        .search-input {
                width: 100%;
        }

        .filter-control {
                flex: 1 1 calc(50% - 0.325rem);
                min-width: 0;
        }

        .header-actions {
                padding: 0.75rem;
                gap: 0.5rem;
        }

        .user-email {
                flex-direction: column;
                align-items: flex-start;
                gap: 0.5rem;
        }

        .pagination-container {
                justify-content: center;
                padding: 0 0.75rem 0.75rem;
        }

        :deep(.user-details-dialog) {
                width: calc(100vw - 1.5rem) !important;
        }
}

@media (max-width: 680px) {
        .admin-users-table {
                display: none;
        }

        .mobile-user-list {
                display: block;
                min-height: 5rem;
        }

        .pagination-container--desktop {
                display: none;
        }

        .pagination-container--mobile {
                display: flex;
        }
}

@media (max-width: 480px) {
        .stats-row :deep(.el-col) {
                max-width: 100%;
                flex: 0 0 100%;
        }

        .stats-card :deep(.el-card__body) {
                padding: 0.85rem;
        }

        .stats-icon {
                margin-right: 0.75rem;
                padding: 0.55rem;
        }

        .filter-control {
                flex-basis: 100%;
                width: 100%;
        }

        .mobile-user-card__header,
        .mobile-user-card__details div {
                align-items: flex-start;
        }

        .mobile-user-card__details div {
                flex-direction: column;
                gap: 0.15rem;
        }

        .mobile-user-card__details strong {
                text-align: left;
        }
}
</style>
