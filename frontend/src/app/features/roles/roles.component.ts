import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { Observable } from 'rxjs';
import { HasPermissionDirective } from '../../core/directives/has-permission.directive';
import { PermisoModulo, Rol } from '../../core/models/seguridad.models';
import { PERMISOS } from '../../core/security/permisos';
import { AuthService } from '../../core/services/auth.service';
import { RolService } from '../../core/services/rol.service';

const CODIGO_ADMIN = 'admin';

/**
 * ABM de roles con su matriz de permisos agrupada por módulo. El rol Administrador siempre tiene todos
 * los permisos y se muestra de solo lectura; los demás roles (incluidos supervisor y cajero) se editan.
 */
@Component({
  selector: 'app-roles',
  standalone: true,
  imports: [
    FormsModule,
    ButtonModule,
    CheckboxModule,
    ConfirmDialogModule,
    DialogModule,
    HasPermissionDirective,
    InputTextModule,
    TableModule,
    TagModule
  ],
  providers: [ConfirmationService],
  templateUrl: './roles.component.html'
})
export class RolesComponent implements OnInit {
  private readonly rolService = inject(RolService);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly authService = inject(AuthService);
  readonly permisos = PERMISOS;

  readonly roles = signal<Rol[]>([]);
  readonly catalogo = signal<PermisoModulo[]>([]);
  readonly cargando = signal(false);

  readonly mostrarFormulario = signal(false);
  readonly guardando = signal(false);
  editando: Rol | null = null;
  nombre = '';
  descripcion = '';
  readonly seleccion = signal<ReadonlySet<string>>(new Set());

  // Método (no computed): 'editando' no es un signal, un computed quedaría cacheado con el primer valor.
  soloLectura(): boolean {
    return this.editando?.codigo === CODIGO_ADMIN && this.editando.esSistema;
  }
  readonly puedeGestionar = computed(() => this.authService.tienePermiso(PERMISOS.RolesGestionar));
  readonly totalSeleccionados = computed(() => this.seleccion().size);

  ngOnInit(): void {
    this.cargar();
    this.rolService.catalogoPermisos().subscribe((catalogo) => this.catalogo.set(catalogo));
  }

  private cargar(): void {
    this.cargando.set(true);
    this.rolService.listar().subscribe({
      next: (roles) => {
        this.roles.set(roles);
        this.cargando.set(false);
      },
      error: () => this.cargando.set(false)
    });
  }

  nuevo(): void {
    this.editando = null;
    this.nombre = '';
    this.descripcion = '';
    this.seleccion.set(new Set());
    this.mostrarFormulario.set(true);
  }

  abrir(rol: Rol): void {
    this.rolService.obtener(rol.id).subscribe((detalle) => {
      this.editando = rol;
      this.nombre = detalle.nombre;
      this.descripcion = detalle.descripcion ?? '';
      this.seleccion.set(new Set(detalle.permisos));
      this.mostrarFormulario.set(true);
    });
  }

  // ---- Selección de permisos ----

  marcado(codigo: string): boolean {
    return this.seleccion().has(codigo);
  }

  alternar(codigo: string): void {
    if (this.soloLectura()) return;
    const nueva = new Set(this.seleccion());
    if (!nueva.delete(codigo)) nueva.add(codigo);
    this.seleccion.set(nueva);
  }

  cuantosDelModulo(modulo: PermisoModulo): number {
    return modulo.permisos.filter((p) => this.seleccion().has(p.codigo)).length;
  }

  moduloCompleto(modulo: PermisoModulo): boolean {
    return this.cuantosDelModulo(modulo) === modulo.permisos.length;
  }

  moduloParcial(modulo: PermisoModulo): boolean {
    const n = this.cuantosDelModulo(modulo);
    return n > 0 && n < modulo.permisos.length;
  }

  alternarModulo(modulo: PermisoModulo): void {
    if (this.soloLectura()) return;
    const nueva = new Set(this.seleccion());
    const completo = this.moduloCompleto(modulo);
    for (const p of modulo.permisos) {
      if (completo) nueva.delete(p.codigo);
      else nueva.add(p.codigo);
    }
    this.seleccion.set(nueva);
  }

  // ---- Persistencia ----

  formularioValido(): boolean {
    return this.nombre.trim().length >= 2;
  }

  guardar(): void {
    if (!this.formularioValido() || this.soloLectura()) return;

    const dto = { nombre: this.nombre.trim(), descripcion: this.descripcion.trim() || null, permisos: [...this.seleccion()] };
    this.guardando.set(true);
    const peticion: Observable<unknown> = this.editando ? this.rolService.actualizar(this.editando.id, dto) : this.rolService.crear(dto);
    peticion.subscribe({
      next: () => {
        this.guardando.set(false);
        this.mostrarFormulario.set(false);
        this.cargar();
        this.messageService.add({
          severity: 'success',
          summary: this.editando ? 'Rol actualizado' : 'Rol creado',
          detail: this.editando ? 'Los usuarios con este rol verán los cambios al renovar su sesión (hasta 15 minutos).' : undefined,
          life: 6000
        });
      },
      error: () => this.guardando.set(false)
    });
  }

  eliminar(rol: Rol): void {
    this.confirmationService.confirm({
      header: 'Eliminar rol',
      message: `¿Eliminar el rol "${rol.nombre}"? Esta acción no se puede deshacer.`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Eliminar',
      rejectLabel: 'Cancelar',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () =>
        this.rolService.eliminar(rol.id).subscribe(() => {
          this.cargar();
          this.messageService.add({ severity: 'success', summary: 'Rol eliminado' });
        })
    });
  }
}
