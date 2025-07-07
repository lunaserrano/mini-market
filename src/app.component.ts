import { Component } from '@angular/core';
import { RouterModule } from '@angular/router';
import { AppSpinner } from './app/shared/app.spinner';

@Component({
    selector: 'app-root',
    standalone: true,
    imports: [RouterModule, AppSpinner],
    template: `
        <app-spinner></app-spinner>
        <router-outlet></router-outlet>
    `
})
export class AppComponent { }
