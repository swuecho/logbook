<template>
        <div class="admin-dashboard">
                <!-- Stats Cards Row -->

                <el-row :gutter="20" class="stats-row">
                        <el-col :span="6">
                                <el-card shadow="hover" class="stats-card">
                                        <div class="stats-icon users-icon">
                                                <i class="el-icon-user"></i>
                                        </div>
                                        <div class="stats-info">
                                                <div class="stats-title">Total Users</div>
                                                <div class="stats-value">{{ users.length }}</div>
                                        </div>
                                </el-card>
                        </el-col>
                        <el-col :span="6">
                                <el-card shadow="hover" class="stats-card">
                                        <div class="stats-icon active-icon">
                                                <i class="el-icon-time"></i>
                                        </div>
                                        <div class="stats-info">
                                                <div class="stats-title">Active Today</div>
                                                <div class="stats-value">{{ activeTodayCount }}</div>
                                        </div>
                                </el-card>
                        </el-col>
                        <el-col :span="6">
                                <el-card shadow="hover" class="stats-card">
                                        <div class="stats-icon diary-icon">
                                                <i class="el-icon-notebook-2"></i>
                                        </div>
                                        <div class="stats-info">
                                                <div class="stats-title">Total Entries</div>
                                                <div class="stats-value">{{ totalEntries }}</div>
                                        </div>
                                </el-card>
                        </el-col>
                        <el-col :span="6">
                                <el-card shadow="hover" class="stats-card">
                                        <div class="stats-icon avg-icon">
                                                <i class="el-icon-data-analysis"></i>
                                        </div>
                                        <div class="stats-info">
                                                <div class="stats-title">Avg. Entries/User</div>
                                                <div class="stats-value">{{ averageEntriesPerUser }}</div>
                                        </div>
                                </el-card>
                        </el-col>
                </el-row>

                <!-- Main Content -->
                <el-card class="box-card">
                        <div slot="header" class="clearfix">
                                <div class="header-actions">
                                        <el-input v-model="searchQuery" placeholder="Search users..."
                                                prefix-icon="el-icon-search" clearable class="search-input"
                                                @clear="handleSearch" @input="handleSearch"></el-input>

                                        <el-select v-model="statusFilter" placeholder="Status" clearable
                                                @change="handleFilter">
                                                <el-option label="Active" value="active"></el-option>
                                                <el-option label="Recent" value="recent"></el-option>
                                                <el-option label="Inactive" value="inactive"></el-option>
                                        </el-select>

                                        <el-select v-model="sortBy" placeholder="Sort by" @change="handleSort">
                                                <el-option label="Newest" value="newest"></el-option>
                                                <el-option label="Most Active" value="mostActive"></el-option>
                                                <el-option label="Most Entries" value="mostEntries"></el-option>
                                        </el-select>
                                </div>
                        </div>

                        <el-table :data="filteredUsers" style="width: 100%" border stripe v-loading="loading"
                                element-loading-text="Loading users...">
                                <el-table-column prop="email" label="Email" min-width="220">
                                        <template slot-scope="scope">
                                                <div class="user-email">
                                                        <span>{{ scope.row.email }}</span>
                                                        <el-button type="text" size="mini"
                                                                @click="viewUserDetails(scope.row)" icon="el-icon-view">
                                                                View Details
                                                        </el-button>
                                                </div>
                                        </template>
                                </el-table-column>

                                <el-table-column label="Diary Count" width="120">
                                        <template slot-scope="scope">
                                                <el-tag type="success" size="medium">
                                                        {{ scope.row.diaryCount }} entries
                                                </el-tag>
                                        </template>
                                </el-table-column>

                                <el-table-column label="Joined Date" width="180">
                                        <template slot-scope="scope">
                                                {{ formatDate(scope.row.dateJoined) }}
                                        </template>
                                </el-table-column>

                                <el-table-column label="Last Login" width="180">
                                        <template slot-scope="scope">
                                                {{ formatDate(scope.row.lastLogin) }}
                                        </template>
                                </el-table-column>

                                <el-table-column label="Status" width="100">
                                        <template slot-scope="scope">
                                                <el-tag :type="getUserStatusType(scope.row)" size="medium">
                                                        {{ getUserStatus(scope.row) }}
                                                </el-tag>
                                        </template>
                                </el-table-column>

                                <el-table-column label="Actions" width="150">
                                        <template slot-scope="scope">
                                                <el-button size="mini" type="danger" icon="el-icon-delete"
                                                        @click="handleDelete(scope.row)"
                                                        :disabled="!canDeleteUser(scope.row)">
                                                        Delete
                                                </el-button>
                                        </template>
                                </el-table-column>
                        </el-table>

                        <div class="pagination-container">
                                <el-pagination @size-change="handleSizeChange" @current-change="handleCurrentChange"
                                        :current-page="currentPage" :page-sizes="[10, 20, 50, 100]"
                                        :page-size="pageSize" layout="total, sizes, prev, pager, next, jumper"
                                        :total="totalUsers">
                                </el-pagination>
                        </div>
                </el-card>

                <!-- User Details Dialog -->
                <el-dialog title="User Details" :visible.sync="dialogVisible" width="50%">
                        <div v-if="selectedUser" class="user-details">
                                <el-descriptions :column="2" border>
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
</template>

<script>
import { format, isToday, differenceInDays, differenceInWeeks } from 'date-fns'
import { fetchUsersWithDiary, deleteUser } from '@/services/users';

export default {
        name: 'AdminDashboard',
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
                        selectedUser: null
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

                        // Apply search
                        if (this.searchQuery) {
                                const query = this.searchQuery.toLowerCase()
                                result = result.filter(user =>
                                        user.email.toLowerCase().includes(query)
                                )
                        }

                        // Apply status filter
                        if (this.statusFilter) {
                                result = result.filter(user =>
                                        this.getUserStatus(user).toLowerCase() === this.statusFilter
                                )
                        }

                        // Apply sorting
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
                        // Only apply pagination here
                        const start = (this.currentPage - 1) * this.pageSize
                        return this.filteredUsersRaw.slice(start, start + this.pageSize)
                },
                totalUsers() {
                        return this.filteredUsersRaw.length
                },
        },
        methods: {
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
                        // Add your logic here to determine if a user can be deleted
                        return true
                },
                async handleDelete(user) {
                        try {
                                await this.$confirm('Are you sure you want to delete this user?', 'Warning', {
                                        confirmButtonText: 'Delete',
                                        cancelButtonText: 'Cancel',
                                        type: 'warning'
                                })

                                await deleteUser(user.id)
                                this.$message.success('User deleted successfully')
                                this.fetchUsers()
                        } catch (error) {
                                if (error !== 'cancel') {
                                        this.$message.error('Failed to delete user')
                                        console.error('Error deleting user:', error)
                                }
                        }
                },
                async fetchUsers() {
                        this.loading = true
                        try {
                                this.users = await fetchUsersWithDiary()
                        } catch (error) {
                                this.$message.error('Failed to fetch users')
                                console.error('Error fetching users:', error)
                        } finally {
                                this.loading = false
                        }
                }
        },
        mounted() {
                this.fetchUsers()
        }
}
</script>

<style scoped>
.admin-dashboard {
        padding: 20px;
        background-color: #f5f7fa;
        min-height: 100vh;
}

.stats-row {
        margin-bottom: 20px;
        margin-left: auto;
        margin-right: auto;
        /* padding: 0 20px; */
        display: flex;
        justify-content: center;
}

.stats-card {
        display: flex;
        align-items: center;
        padding: 20px;
}

.stats-icon {
        font-size: 48px;
        margin-right: 20px;
        padding: 15px;
        border-radius: 8px;
}

.users-icon {
        background-color: #ecf5ff;
        color: #409EFF;
}

.active-icon {
        background-color: #f0f9eb;
        color: #67c23a;
}

.diary-icon {
        background-color: #fdf6ec;
        color: #e6a23c;
}

.avg-icon {
        background-color: #fef0f0;
        color: #f56c6c;
}

.stats-info {
        flex: 1;
}

.stats-title {
        font-size: 14px;
        color: #909399;
        margin-bottom: 8px;
}

.stats-value {
        font-size: 24px;
        font-weight: bold;
        color: #303133;
}



.header-actions {
        display: flex;
        gap: 10px;
        align-items: center;
        padding: 20px 20px 0 20px;
}

.search-input {
        width: 300px;
}

.user-email {
        display: flex;
        justify-content: space-between;
        align-items: center;
}

.pagination-container {
        margin-top: 20px;
        display: flex;
        justify-content: flex-end;
        padding: 0 20px 20px 20px;
}

.user-details {
        padding: 20px;
}
</style>
