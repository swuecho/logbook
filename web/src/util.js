import moment from 'moment';

export function get_date_of_today() {
    return moment().format('YYYYMMDD')
}