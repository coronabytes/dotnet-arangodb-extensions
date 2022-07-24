import { Component } from '@angular/core';
import * as AspNetData from 'devextreme-aspnet-data-nojquery';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent {
  dataSource: any;

  constructor() {
    this.dataSource = AspNetData.createStore({
      key: 'id',
      loadUrl: '/api/grid/arango'
    });
  }
}
