import { CanActivateFn, Router } from '@angular/router';

export function roleGuard(roles: string[]): CanActivateFn {
    return () => {
        const jwt = localStorage.getItem('jwt');
        if (!jwt) {
            window.location.href = '/pages/auth/login';
            return false;
        }
        const user = JSON.parse(jwt);
        if (!roles.includes(user.rol)) {
            window.location.href = '/';
            return false;
        }
        return true;
    };
}
